import test from 'node:test';
import assert from 'node:assert/strict';
import { generateKeyPairSync, sign } from 'node:crypto';
import { registerMatch, completeMatch, standings, verifyUnityToken, PROJECT } from '../../netlify/lib/history-service.mjs';
class MemoryStore {
  data=new Map(); revision=0;
  async get(k) { return structuredClone(this.data.get(k)?.data ?? null); }
  async getWithMetadata(k) { return structuredClone(this.data.get(k) ?? null); }
  async setJSON(k,data,options={}) {
    const previous=this.data.get(k);
    if(options.onlyIfNew && previous || options.onlyIfMatch && previous?.etag!==options.onlyIfMatch) return {modified:false};
    const etag=String(++this.revision); this.data.set(k,{data:structuredClone(data),etag}); return {modified:true,etag};
  }
}
const id='0123456789abcdef0123456789abcdef';
const setup=()=>({MatchId:id,MapId:'bridge',PointsToWin:6,Hearts:1,IsAutomated:false,Roster:[{CharacterId:'matej',Name:'One',IsBot:false},{CharacterId:'nik',Name:'Two',IsBot:false}]});
const result=()=>({Completed:true,RoundCount:2,FinalHash:'abcdef01',Record:{MatchId:id,MapId:'bridge',Players:[
 {CharacterId:'matej',Name:'One',Place:1,Points:8,RoundWins:2,Kills:2,Deaths:0,Badges:[]},
 {CharacterId:'nik',Name:'Two',Place:2,Points:0,RoundWins:0,Kills:0,Deaths:2,Badges:[]}]}});
test('eligibility rejects all bot rosters, one human and automated production games',async()=>{
 for(const roster of [[setup().Roster[0]],[...setup().Roster,{CharacterId:'matic',Name:'Bot',IsBot:true}],[{...setup().Roster[0],IsBot:true},setup().Roster[1]]])
   await assert.rejects(registerMatch(new MemoryStore(),{...setup(),Roster:roster},'host',false,1000));
 await assert.rejects(registerMatch(new MemoryStore(),{...setup(),IsAutomated:true},'host',false,1000));
 await registerMatch(new MemoryStore(),{...setup(),IsAutomated:true},'host',true,1000);
});
test('only registered host can complete, results immutable and concurrent retries count once',async()=>{
 const store=new MemoryStore(); await registerMatch(store,setup(),'host',false,1000);
 await assert.rejects(completeMatch(store,result(),'guest',30000),e=>e.status===403);
 const writes=await Promise.all([completeMatch(store,result(),'host',30000),completeMatch(store,result(),'host',30000)]);
 assert.equal(writes.filter(Boolean).length,1);
 assert.equal(await completeMatch(store,result(),'host',40000),false);
 assert.equal((await store.get('matches/'+id)).CompletedUtc,new Date(30000).toISOString());
 const altered=result();altered.Record.Players[0].Points++;
 await assert.rejects(completeMatch(store,altered,'host',40000),e=>e.status===409);
 assert.equal(standings([await store.get('matches/'+id)])[0].Wins,1);
});
test('aborted/changed/too early submissions rejected, new match ID is independent',async()=>{
 const store=new MemoryStore(); await registerMatch(store,setup(),'host',false,1000);
 await assert.rejects(completeMatch(store,{...result(),Completed:false},'host',30000));
 await assert.rejects(completeMatch(store,result(),'host',2000));
 const renamed=result();renamed.Record.Players[0].Name='Impostor';await assert.rejects(completeMatch(store,renamed,'host',30000));
 await assert.rejects(registerMatch(store,setup(),'another-host',false,30000));
 await registerMatch(store,{...setup(),MatchId:'f'.repeat(32)},'host',false,30000);
});
test('leaderboard uses recency for equal victories, co-winners use character ID and zero wins stable',()=>{
 const rows=[{CompletedUtc:'2026-10-24T10:00:00Z',Players:[{CharacterId:'matej',Place:1}]},
 {CompletedUtc:'2026-10-24T11:00:00Z',Players:[{CharacterId:'nik',Place:1}]}];
 assert.deepEqual(standings(rows).map(x=>x.CharacterId),['nik','matej','aljaz','matic']);
 rows[1].Players.push({CharacterId:'matic',Place:1});
 assert.deepEqual(standings(rows).slice(0,2).map(x=>x.CharacterId),['matic','nik']);
});
test('Unity token must have genuine signature, project, issuer and current lifetime',()=>{
 const {privateKey,publicKey}=generateKeyPairSync('rsa',{modulusLength:2048});
 const key={...publicKey.export({format:'jwk'}),kid:'test',alg:'RS256'};
 const token=(claims)=>{const data=[{alg:'RS256',kid:'test'},claims].map(x=>Buffer.from(JSON.stringify(x)).toString('base64url')).join('.');return data+'.'+sign('RSA-SHA256',Buffer.from(data),privateKey).toString('base64url');};
 const claims={iss:'https://player-auth.services.api.unity.com',project_id:PROJECT,sub:'player',exp:300};
 assert.equal(verifyUnityToken(token(claims),[key],100000),'player');
 for(const invalid of [{...claims,project_id:'other'},{...claims,iss:'fake'},{...claims,exp:1},{...claims,nbf:200}]) assert.throws(()=>verifyUnityToken(token(invalid),[key],100000));
 assert.throws(()=>verifyUnityToken(token(claims).slice(0,-10)+'tampered',[key],100000));
});
test('equal point totals can be broken by authoritative round-history rankings',async()=>{
 const store=new MemoryStore();await registerMatch(store,setup(),'host',false,1000);
 const value=result();value.Record.Players[1].Points=8;
 assert.equal(await completeMatch(store,value,'host',30000),true);
});
