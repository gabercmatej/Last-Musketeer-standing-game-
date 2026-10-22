using System.Collections.Generic;
using Musketeers.Game;
using Musketeers.Net;
using Musketeers.Sim;
using NUnit.Framework;

namespace Musketeers.Sim.Tests
{
    // Day 5 chunk B: the online protocol (NetTransport) over an in-process fake wire, with a host and two clients
    // (plus one bot) playing whole matches, plus the snapshot serializer, plan codec and chat rules.
    public class NetMatchTests
    {
        // ---------- fake wire ----------

        sealed class Hub
        {
            public readonly List<Machine> Machines = new List<Machine>();
            readonly Queue<System.Action> _queue = new Queue<System.Action>();
            public void Post(System.Action a) => _queue.Enqueue(a);
            public void Pump()
            {
                while (_queue.Count > 0)
                    _queue.Dequeue()();
            }
            public Machine Host => Machines[0];
        }

        sealed class Machine : INetLink
        {
            public ulong Id;
            public Hub Hub;
            public NetTransport T;
            public MatchController Mc;
            public bool Gone;
            public int PlannedTurn;
            public BotPlanner Auto;
            public readonly List<string> Warnings = new List<string>();
            public readonly List<TurnPlan[]> Resolved = new List<TurnPlan[]>();

            Machine HostM => Hub.Host;
            void ToHost(System.Action<Machine> a) => Hub.Post(() => { if (!Gone) a(HostM); });
            void ToClients(System.Action<Machine> a)
            {
                foreach (var m in Hub.Machines)
                {
                    if (m == this)
                        continue;
                    var c = m;
                    Hub.Post(() => { if (!c.Gone) a(c); });
                }
            }

            public void SendReady() { var id = Id; ToHost(h => h.T.HostOnReady(id)); }
            public void SendPlan(int turnSeq, TurnPlan plan, bool locked)
            {
                var id = Id; var bytes = PlanCodec.Encode(plan);
                ToHost(h => h.T.HostOnPlan(id, turnSeq, PlanCodec.Decode(bytes), locked));
            }
            public void SendTurnDone(int turnSeq, uint hash) { var id = Id; ToHost(h => h.T.HostOnTurnDone(id, turnSeq, hash)); }
            public void BroadcastBegin(int turnSeq, int ms) => ToClients(c => c.T.ClientOnBegin(turnSeq, ms));
            public void BroadcastLock(int turnSeq, int mask) => ToClients(c => c.T.ClientOnLock(turnSeq, mask));
            public void BroadcastResolve(int turnSeq, TurnPlan[] plans)
            {
                var bytes = PlanCodec.Encode(plans);
                ToClients(c => c.T.ClientOnResolve(turnSeq, PlanCodec.DecodeAll(bytes)));
            }
            public void SendSnapshot(ulong clientId, int turnSeq, byte[] data)
            {
                foreach (var m in Hub.Machines)
                {
                    var c = m;
                    if (c.Id == clientId)
                        Hub.Post(() => { if (!c.Gone) c.T.ClientOnSnapshot(turnSeq, data); });
                }
            }
        }

        static MatchSetup MakeSetup(string ascii, int localSlot, uint seed)
        {
            var abilities = new[] { Ability.Grappin, Ability.Switcheroo, Ability.TripleCharge, Ability.BullRush };
            var cfg = new GameConfig { PointsToWin = 6 };
            return new MatchSetup
            {
                MapAscii = ascii,
                Config = cfg,
                Seed = seed,
                Abilities = abilities,
                Characters = new[] { 0, 1, 2, 3 },
                Names = new[] { "Host", "B", "C", "Bot" },
                IsBot = new[] { false, false, false, true },
                Loadouts = new[] { "", "", "", "" },
                LocalSlot = localSlot,
            };
        }

        static Hub Build(string ascii, uint seed, float planningSeconds = 0f)
        {
            var hub = new Hub();
            var slotClients = new ulong[] { 0, 1, 2, ulong.MaxValue };
            for (int i = 0; i < 3; i++)
            {
                var m = new Machine { Id = (ulong)i, Hub = hub };
                m.T = new NetTransport(m, i == 0, slotClients);
                if (i == 0)
                    m.T.PlanningSecondsOverride = planningSeconds;
                var mm = m;
                m.T.Warn += w => mm.Warnings.Add(w);
                m.T.SnapshotReceived += s => mm.Mc.ApplySnapshot(s);
                m.Mc = new MatchController(MakeSetup(ascii, i, seed), m.T);
                m.T.PlansResolved += (t, p) => mm.Resolved.Add(p);
                hub.Machines.Add(m);
            }
            return hub;
        }

        // One simulation step: deliver messages, tick, let each human autoplay its own slot, finish playback.
        static void Step(Hub hub, float dt, bool autoplay, System.Action<Machine> beforeDone = null)
        {
            hub.Pump();
            foreach (var m in hub.Machines)
            {
                if (m.Gone)
                    continue;
                m.Mc.Update(dt);
                hub.Pump();
                if (autoplay && m.Mc.Phase == MatchPhase.Planning && m.PlannedTurn != m.Mc.TurnSeq)
                {
                    m.PlannedTurn = m.Mc.TurnSeq;
                    var mc = m.Mc;
                    m.Auto = m.Auto ?? new BotPlanner(mc.Setup.LocalSlot, 99u + (uint)mc.Setup.LocalSlot);
                    var plan = m.Auto.Plan(mc.Match.Round, mc.Match.Cfg, mc.Match.RoundNumber);
                    plan.Slot = (byte)mc.Setup.LocalSlot;
                    mc.SubmitPlan(plan, true);
                }
                if (m.Mc.Phase == MatchPhase.Playback)
                {
                    beforeDone?.Invoke(m);
                    m.Mc.PlaybackFinished();
                }
                else if (m.Mc.Phase == MatchPhase.RoundOver)
                    m.Mc.ContinueAfterRound();
                hub.Pump();
            }
        }

        static void StartAll(Hub hub)
        {
            // Clients first or host first: both orders must work (the host waits for every Ready).
            for (int i = hub.Machines.Count - 1; i >= 0; i--)
                hub.Machines[i].Mc.Start();
            hub.Pump();
        }

        static bool AllOver(Hub hub)
        {
            foreach (var m in hub.Machines)
            {
                if (!m.Gone && m.Mc.Phase != MatchPhase.MatchOver)
                    return false;
            }
            return true;
        }

        static void RunToEnd(Hub hub, System.Action<Machine> beforeDone = null, System.Action<int> perStep = null)
        {
            for (int i = 0; i < 40000 && !AllOver(hub); i++)
            {
                Step(hub, 0.1f, true, beforeDone);
                perStep?.Invoke(i);
            }
            Assert.That(AllOver(hub), "match never ended");
        }

        // ---------- full matches ----------

        [TestCase(BuiltInMaps.Courtyard)]
        [TestCase(BuiltInMaps.Bridge)]
        public void FullMatch_HostAndTwoClients_EndWithIdenticalState(string map)
        {
            var hub = Build(map, 4242u);
            StartAll(hub);
            RunToEnd(hub);
            uint h = hub.Host.Mc.Match.ComputeHash();
            foreach (var m in hub.Machines)
            {
                Assert.That(m.Mc.Match.ComputeHash(), Is.EqualTo(h), $"machine {m.Id}");
                Assert.That(m.Warnings, Is.Empty, $"machine {m.Id} warned: " + string.Join("; ", m.Warnings));
            }
            Assert.That(hub.Host.Mc.Match.MatchOver);
        }

        [Test]
        public void OnlineHostMatchesOffline_SamePlansSameState()
        {
            // The host's plans (humans + bot) replayed through plain MatchRunner give the same final state.
            var hub = Build(BuiltInMaps.Courtyard, 77u);
            StartAll(hub);
            RunToEnd(hub);
            var setup = MakeSetup(BuiltInMaps.Courtyard, 0, 77u);
            var m = MatchRunner.NewMatch(MapParser.Parse(setup.MapAscii), setup.Abilities, setup.Config, setup.Seed, out _, setup.Characters);
            foreach (var plans in hub.Host.Resolved)
                MatchRunner.PlayTurn(m, plans);
            Assert.That(m.ComputeHash(), Is.EqualTo(hub.Host.Mc.Match.ComputeHash()));
        }

        [Test]
        public void TimerExpiry_ResolvesWithoutLocks_AfterGrace()
        {
            var hub = Build(BuiltInMaps.Courtyard, 5u, planningSeconds: 1f);
            StartAll(hub);
            hub.Pump();
            Assert.That(hub.Host.Mc.Phase, Is.EqualTo(MatchPhase.Planning));
            Assert.That(hub.Machines[1].Mc.Phase, Is.EqualTo(MatchPhase.Planning));
            Assert.That(hub.Machines[1].Mc.PlanningSecondsLeft, Is.EqualTo(1f).Within(0.01f));
            for (int i = 0; i < 10; i++)
                Step(hub, 0.1f, false);   // 1.0 s: countdown over, grace not yet
            Assert.That(hub.Host.Resolved.Count, Is.EqualTo(0));
            for (int i = 0; i < 6; i++)
                Step(hub, 0.1f, false);
            Assert.That(hub.Host.Resolved.Count, Is.EqualTo(1));
            hub.Pump();
            Assert.That(hub.Machines[2].Mc.Match.ComputeHash(), Is.EqualTo(hub.Host.Mc.Match.ComputeHash()));
        }

        [Test]
        public void StalePlan_FromOldTurn_IsIgnored()
        {
            var hub = Build(BuiltInMaps.Courtyard, 9u);
            StartAll(hub);
            var host = hub.Host;
            int turn = host.Mc.TurnSeq;
            var plan = TurnPlan.AllCatchBreath(1);
            host.T.HostOnPlan(1, turn - 1, plan, true);
            Assert.That(host.Mc.LockMask & 2, Is.EqualTo(0));
            host.T.HostOnPlan(1, turn, plan, true);
            Assert.That(host.Mc.LockMask & 2, Is.EqualTo(2));
            // A client cannot plan another slot: the slot comes from its client id.
            var spoof = TurnPlan.AllCatchBreath(0);
            host.T.HostOnPlan(2, turn, spoof, true);
            Assert.That(host.Mc.LockMask & 1, Is.EqualTo(0));
            Assert.That(host.Mc.LockMask & 4, Is.EqualTo(4));
        }

        // ---------- desync -> snapshot ----------

        [Test]
        public void ForcedDesync_HostSendsSnapshot_AndHashesMatchAgain()
        {
            var hub = Build(BuiltInMaps.Bridge, 31337u);
            StartAll(hub);
            bool corrupted = false;
            RunToEnd(hub, m =>
            {
                if (!corrupted && m.Id == 1 && m.Mc.TurnSeq == 3)
                {
                    corrupted = true;
                    m.Mc.Match.Round.Units[0].Hp += 1;   // this machine's state silently diverges
                    m.Mc.Match.Points[2] += 3;
                }
            });
            Assert.That(corrupted);
            Assert.That(hub.Host.Warnings.Exists(w => w == "DESYNC turn 3 slot 1"), "host should log the desync: " + string.Join("; ", hub.Host.Warnings));
            uint h = hub.Host.Mc.Match.ComputeHash();
            foreach (var m in hub.Machines)
                Assert.That(m.Mc.Match.ComputeHash(), Is.EqualTo(h), $"machine {m.Id}");
        }

        // ---------- disconnect ----------

        [Test]
        public void DisconnectedHuman_PlaysCatchBreath_MatchStillEnds()
        {
            var hub = Build(BuiltInMaps.Courtyard, 2024u);
            StartAll(hub);
            var leaver = hub.Machines[2];
            int leftAtTurn = -1;
            int step = 0;
            while (!AllOver(hub) && step++ < 40000)
            {
                Step(hub, 0.1f, true);
                if (leftAtTurn < 0 && hub.Host.Mc.TurnSeq >= 3 && hub.Host.Mc.Phase == MatchPhase.Planning)
                {
                    leftAtTurn = hub.Host.Mc.TurnSeq;
                    leaver.Gone = true;
                    hub.Host.T.ClientDisconnected(leaver.Id);
                }
            }
            Assert.That(AllOver(hub), "match never ended");
            Assert.That(leftAtTurn, Is.GreaterThan(0));
            // From the turn they left on, every resolved plan for slot 2 is Catch Breath x3.
            int checkedTurns = 0;
            for (int i = leftAtTurn - 1; i < hub.Host.Resolved.Count; i++)
            {
                var p = hub.Host.Resolved[i][2];
                Assert.That(p.A0.Type, Is.EqualTo(ActionType.CatchBreath), $"turn {i + 1}");
                Assert.That(p.A1.Type, Is.EqualTo(ActionType.CatchBreath));
                Assert.That(p.A2.Type, Is.EqualTo(ActionType.CatchBreath));
                checkedTurns++;
            }
            Assert.That(checkedTurns, Is.GreaterThan(0));
            Assert.That(hub.Machines[1].Mc.Match.ComputeHash(), Is.EqualTo(hub.Host.Mc.Match.ComputeHash()));
        }

        // ---------- snapshot serializer, codec ----------

        [Test]
        public void MatchStateSerializer_RoundTrip_EveryTurn_SameHash()
        {
            foreach (string map in new[] { BuiltInMaps.Courtyard, BuiltInMaps.Bridge })
            {
                var setup = MakeSetup(map, 0, 555u);
                var m = MatchRunner.NewMatch(MapParser.Parse(map), setup.Abilities, setup.Config, 555u, out _, setup.Characters);
                var bots = new BotPlanner[4];
                for (int i = 0; i < 4; i++)
                    bots[i] = new BotPlanner(i, (uint)(100 + i));
                int turns = 0;
                while (!m.MatchOver && turns++ < 3000)
                {
                    var plans = new TurnPlan[4];
                    for (int i = 0; i < 4; i++)
                        plans[i] = bots[i].Plan(m.Round, m.Cfg, m.RoundNumber);
                    MatchRunner.PlayTurn(m, plans);
                    var bytes = MatchStateSerializer.Serialize(m);
                    Assert.That(MatchStateSerializer.Serialize(m), Is.EqualTo(bytes), "serialize must be deterministic");
                    var back = MatchStateSerializer.Deserialize(bytes, m);
                    Assert.That(back.ComputeHash(), Is.EqualTo(m.ComputeHash()), $"turn {turns}");
                    Assert.That(MatchStateSerializer.Serialize(back), Is.EqualTo(bytes), $"re-serialize turn {turns}");
                }
                Assert.That(m.MatchOver);
            }
        }

        [Test]
        public void PlanCodec_RoundTrips()
        {
            var p = new TurnPlan(2);
            p.A0 = PlannedAction.Move(Dir.W);
            p.A1 = PlannedAction.ThrowKeg(new Int2(11, 7));
            p.A2 = PlannedAction.Switcheroo(3);
            var back = PlanCodec.Decode(PlanCodec.Encode(p));
            Assert.That(back.Slot, Is.EqualTo(2));
            Assert.That(back.A0.Type, Is.EqualTo(ActionType.Move));
            Assert.That(back.A0.Dir, Is.EqualTo(Dir.W));
            Assert.That(back.A1.Target, Is.EqualTo(new Int2(11, 7)));
            Assert.That(back.A2.Unit, Is.EqualTo(3));
            var all = PlanCodec.DecodeAll(PlanCodec.Encode(new[] { p, TurnPlan.AllCatchBreath(1) }));
            Assert.That(all.Length, Is.EqualTo(2));
            Assert.That(all[1].A2.Type, Is.EqualTo(ActionType.CatchBreath));
        }

        // ---------- chat ----------

        [Test]
        public void ChatRelay_LimitsLengthAndRate()
        {
            var r = new ChatRelay();
            Assert.That(r.Accept(1, new string('x', 200), -1, 0f, out string clean));
            Assert.That(clean.Length, Is.EqualTo(80));
            Assert.That(r.Accept(1, "again", -1, 0.5f, out _), Is.False, "second message within a second is dropped");
            Assert.That(r.Accept(2, "other sender", -1, 0.5f, out _), "limit is per sender");
            Assert.That(r.Accept(1, "ok", -1, 1.0f, out clean));
            Assert.That(clean, Is.EqualTo("ok"));
            Assert.That(r.Accept(3, "   ", -1, 5f, out _), Is.False, "empty is dropped");
            Assert.That(r.Accept(3, "line\nbreak\t", -1, 6f, out clean));
            Assert.That(clean, Is.EqualTo("linebreak"));
            Assert.That(r.Accept(4, null, 2, 7f, out _), "emote needs no text");
            Assert.That(r.Accept(5, null, 9, 7f, out _), Is.False, "unknown emote");
            Assert.That(r.Accept(4, null, 1, 7.5f, out _), Is.False, "emotes share the rate limit");
        }
    }
}
