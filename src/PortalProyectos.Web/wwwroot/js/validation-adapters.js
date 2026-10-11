(() => {
  "use strict";

  if (!window.jQuery?.validator?.unobtrusive) return;

  window.jQuery.validator.addMethod("mustbetrue", (_value, element) => element.checked);
  window.jQuery.validator.unobtrusive.adapters.addBool("mustbetrue");
})();
