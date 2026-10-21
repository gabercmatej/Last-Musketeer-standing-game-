// FIXES_4E §7: fullscreen through the browser Fullscreen API on the page container (#game), not on the canvas, so
// the canvas always fills the real window. The container has CSS 100% x 100% in fullscreen and Unity sizes its
// drawing buffer from the canvas (devicePixelRatio included), then the game recomputes camera fit and UI layout.
var MusketeersFullscreen = {
  Musketeers_IsFullscreen: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  },

  // Unity delivers pointer events a frame late, outside the browser's user gesture. If the page has no active user
  // activation any more (Safari is strict), the request waits for the next real pointerup / keydown and runs there.
  Musketeers_ToggleFullscreen: function () {
    var doc = document;
    var container = doc.getElementById('game') || doc.documentElement;
    var isFs = function () { return doc.fullscreenElement || doc.webkitFullscreenElement; };
    var notify = function () {
      // Let Unity re-measure the canvas, twice: once now and once after the browser finished the transition.
      window.dispatchEvent(new Event('resize'));
      setTimeout(function () { window.dispatchEvent(new Event('resize')); }, 250);
    };
    if (!doc.__musketeersFsHooked) {
      doc.__musketeersFsHooked = true;
      doc.addEventListener('fullscreenchange', notify);
      doc.addEventListener('webkitfullscreenchange', notify);
      window.addEventListener('resize', function () {
        var c = doc.getElementById('unity-canvas');
        if (c) { c.style.width = '100%'; c.style.height = '100%'; }
      });
    }
    var act = function () {
      if (isFs()) {
        var exit = doc.exitFullscreen || doc.webkitExitFullscreen;
        if (exit) exit.call(doc);
      } else {
        var req = container.requestFullscreen || container.webkitRequestFullscreen;
        if (req) {
          var p = req.call(container);
          if (p && p.catch) p.catch(function () {});
        }
      }
    };
    if (navigator.userActivation && !navigator.userActivation.isActive) {
      var once = function () {
        doc.removeEventListener('pointerup', once, true);
        doc.removeEventListener('keydown', once, true);
        act();
      };
      doc.addEventListener('pointerup', once, true);
      doc.addEventListener('keydown', once, true);
    } else {
      act();
    }
  }
};
mergeInto(LibraryManager.library, MusketeersFullscreen);
