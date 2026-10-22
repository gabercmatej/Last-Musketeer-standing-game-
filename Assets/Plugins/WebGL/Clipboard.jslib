// Day 5: copy text from the game (lobby code, invite link). GUIUtility.systemCopyBuffer does nothing in WebGL, so
// the browser clipboard is used: navigator.clipboard.writeText first, then the old execCommand('copy') on a hidden
// textarea (insecure origins, strict Safari). If neither is allowed outside a user gesture, the copy is retried once
// on the player's next click / key press.
var MusketeersClipboard = {
  Musketeers_CopyToClipboard: function (textPtr) {
    var text = UTF8ToString(textPtr);
    var legacy = function () {
      try {
        var ta = document.createElement('textarea');
        ta.value = text;
        ta.setAttribute('readonly', '');
        ta.style.position = 'fixed';
        ta.style.top = '0';
        ta.style.left = '0';
        ta.style.opacity = '0';
        document.body.appendChild(ta);
        ta.focus();
        ta.select();
        var ok = document.execCommand('copy');
        document.body.removeChild(ta);
        return ok;
      } catch (e) {
        return false;
      }
    };
    var retryOnNextGesture = function () {
      var once = function () {
        window.removeEventListener('pointerup', once, true);
        window.removeEventListener('keydown', once, true);
        if (!legacy() && navigator.clipboard && navigator.clipboard.writeText) {
          navigator.clipboard.writeText(text).catch(function () {});
        }
      };
      window.addEventListener('pointerup', once, true);
      window.addEventListener('keydown', once, true);
    };
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).catch(function () {
        if (!legacy()) retryOnNextGesture();
      });
    } else if (!legacy()) {
      retryOnNextGesture();
    }
  }
};

mergeInto(LibraryManager.library, MusketeersClipboard);
