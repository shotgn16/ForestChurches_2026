// Forest Churches — small progressive enhancements shared by every page.
// Everything here is attached with addEventListener (the site's CSP blocks inline handlers),
// and every page still works if this file fails to load.
(function () {
    'use strict';

    function ready(fn) {
        if (document.readyState !== 'loading') { fn(); } else { document.addEventListener('DOMContentLoaded', fn); }
    }

    ready(function () {
        // Mobile nav toggle
        document.querySelectorAll('[data-fc-nav-toggle]').forEach(function (btn) {
            var target = document.getElementById(btn.getAttribute('aria-controls'));
            if (!target) { return; }
            btn.addEventListener('click', function () {
                var open = target.classList.toggle('is-open');
                btn.setAttribute('aria-expanded', open ? 'true' : 'false');
            });
        });

        // Show / hide password
        document.querySelectorAll('[data-fc-pw-toggle]').forEach(function (btn) {
            var input = document.getElementById(btn.getAttribute('aria-controls'));
            if (!input) { return; }
            var showLabel = btn.getAttribute('data-show') || 'Show';
            var hideLabel = btn.getAttribute('data-hide') || 'Hide';
            btn.addEventListener('click', function () {
                var hidden = input.type === 'password';
                input.type = hidden ? 'text' : 'password';
                btn.textContent = hidden ? hideLabel : showLabel;
                btn.setAttribute('aria-pressed', hidden ? 'true' : 'false');
            });
        });

        // Confirm before submitting dangerous forms: <form data-fc-confirm="Are you sure?">
        document.querySelectorAll('form[data-fc-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                if (!window.confirm(form.getAttribute('data-fc-confirm'))) { e.preventDefault(); }
            });
        });

        // Light / dark toggle. Saves the choice in a cookie (read by the server on the next page) and localStorage.
        document.querySelectorAll('[data-fc-theme-toggle]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var root = document.documentElement;
                var current = root.getAttribute('data-theme') ||
                    (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
                var next = current === 'dark' ? 'light' : 'dark';
                root.setAttribute('data-theme', next);
                document.cookie = 'fc-theme=' + next + '; path=/; max-age=31536000; samesite=lax';
                try { window.localStorage.setItem('fc-theme', next); } catch (e) { /* storage blocked */ }
                document.dispatchEvent(new CustomEvent('fc:themechange', { detail: next }));
            });
        });

        // Show/hide parts of a form by a select's value:
        // <select data-fc-type-switch> plus <div data-fc-show-for="Bulletin,Other">. Hidden fields still post.
        document.querySelectorAll('select[data-fc-type-switch]').forEach(function (sel) {
            function apply() {
                document.querySelectorAll('[data-fc-show-for]').forEach(function (el) {
                    el.hidden = el.getAttribute('data-fc-show-for').split(',').indexOf(sel.value) < 0;
                });
            }
            sel.addEventListener('change', apply);
            apply();
        });

        // Print buttons: <button data-fc-print>
        document.querySelectorAll('[data-fc-print]').forEach(function (btn) {
            btn.addEventListener('click', function () { window.print(); });
        });

        // Tabs: <div data-fc-tabs> with [role=tab][aria-controls] buttons and [role=tabpanel] panels
        document.querySelectorAll('[data-fc-tabs]').forEach(function (root) {
            var tabs = Array.prototype.slice.call(root.querySelectorAll('[role="tab"]'));
            if (!tabs.length) { return; }
            root.classList.add('fc-tabs-ready');
            function select(tab) {
                tabs.forEach(function (t) {
                    var on = t === tab;
                    t.setAttribute('aria-selected', on ? 'true' : 'false');
                    t.tabIndex = on ? 0 : -1;
                    var panel = document.getElementById(t.getAttribute('aria-controls'));
                    if (panel) { panel.hidden = !on; }
                });
            }
            tabs.forEach(function (tab, i) {
                tab.addEventListener('click', function () { select(tab); });
                tab.addEventListener('keydown', function (e) {
                    if (e.key === 'ArrowRight' || e.key === 'ArrowLeft') {
                        var next = tabs[(i + (e.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
                        next.focus(); select(next);
                    }
                });
            });
            select(tabs[0]);
        });

        // "Use my location": <button data-fc-geo="/Page?handler=GetAddress" data-fc-geo-target="inputId,otherId">
        document.querySelectorAll('[data-fc-geo]').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var targets = (btn.getAttribute('data-fc-geo-target') || '').split(',')
                    .map(function (id) { return document.getElementById(id.trim()); })
                    .filter(Boolean);
                function setAll(value) { targets.forEach(function (t) { t.value = value; }); }
                if (!navigator.geolocation) { setAll(''); alert('Your browser can’t share your location.'); return; }
                var original = btn.textContent;
                btn.disabled = true;
                btn.textContent = btn.getAttribute('data-busy') || 'Finding you…';
                navigator.geolocation.getCurrentPosition(function (pos) {
                    var token = document.querySelector('input[name="__RequestVerificationToken"]');
                    var url = btn.getAttribute('data-fc-geo') +
                        '&latitude=' + encodeURIComponent(pos.coords.latitude) +
                        '&longitude=' + encodeURIComponent(pos.coords.longitude);
                    fetch(url, { headers: token ? { 'X-CSRF-TOKEN-HEADER': token.value } : {}, credentials: 'same-origin' })
                        .then(function (r) { if (!r.ok) { throw new Error(r.status); } return r.text(); })
                        .then(function (address) { setAll(address.replace(/^"|"$/g, '')); })
                        .catch(function () { alert('Sorry, we couldn’t find your address. Please type it instead.'); })
                        .finally(function () { btn.disabled = false; btn.textContent = original; });
                }, function () {
                    btn.disabled = false; btn.textContent = original;
                    alert('Location permission was declined. Please type your postcode instead.');
                });
            });
        });
    });

    // QR codes (authenticator setup): <div data-fc-qr="otpauth://..."> rendered once qrcode.js has loaded.
    window.addEventListener('load', function () {
        if (typeof window.QRCode === 'undefined') { return; }
        document.querySelectorAll('[data-fc-qr]').forEach(function (el) {
            var text = el.getAttribute('data-fc-qr');
            if (!text) { return; }
            new window.QRCode(el, { text: text, width: 168, height: 168, colorDark: '#1F3D2B', colorLight: '#ffffff', correctLevel: window.QRCode.CorrectLevel.M });
        });
    });
})();
