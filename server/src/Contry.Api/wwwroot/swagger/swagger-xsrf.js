(function () {
  const storageKey = 'contry.swagger.xsrf';

  function getXsrfInput() {
    return document.querySelector('.dialog-ux input[type="text"]');
  }

  function hydrateXsrfInput() {
    const input = getXsrfInput();
    const storedValue = window.localStorage.getItem(storageKey);

    if (!input || !storedValue || input.value === storedValue) {
      return;
    }

    input.value = storedValue;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function persistXsrfValue() {
    const input = getXsrfInput();

    if (!input) {
      return;
    }

    const value = input.value.trim();

    if (!value) {
      window.localStorage.removeItem(storageKey);
      return;
    }

    window.localStorage.setItem(storageKey, value);
  }

  document.addEventListener('click', function (event) {
    const target = event.target;

    if (!(target instanceof HTMLElement)) {
      return;
    }

    const button = target.closest('button');

    if (!button) {
      return;
    }

    window.setTimeout(function () {
      const isDialogButton = !!button.closest('.dialog-ux');
      const label = (button.textContent || '').trim();

      if (isDialogButton && (label === 'Authorize' || label === 'Close')) {
        persistXsrfValue();
      }

      if (label === 'Authorize') {
        hydrateXsrfInput();
      }
    }, 0);
  });

  document.addEventListener('input', function (event) {
    if (event.target instanceof HTMLInputElement && event.target.closest('.dialog-ux')) {
      persistXsrfValue();
    }
  });
})();
