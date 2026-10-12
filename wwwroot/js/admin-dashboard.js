// Site admin dashboard: live CPU / memory / database figures from /api/SystemStats.
// Polls every 10 seconds while the tab is visible (the old page polled every 3s and filled the logs).
(function () {
    'use strict';
    var root = document.getElementById('fc-system-stats');
    if (!root || !window.fetch) { return; }

    var endpoint = root.getAttribute('data-endpoint') || '/api/SystemStats';
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    var errorNote = document.getElementById('fc-stats-error');
    var timer = null;

    function set(id, text) {
        var el = document.getElementById(id);
        if (el) { el.textContent = text; }
    }

    function num(value) { return typeof value === 'number' && isFinite(value) ? value : null; }

    function refresh() {
        var headers = tokenInput ? { 'RequestVerificationToken': tokenInput.value } : {};
        fetch(endpoint, { headers: headers, credentials: 'same-origin', cache: 'no-store' })
            .then(function (r) { if (!r.ok) { throw new Error(r.status); } return r.json(); })
            .then(function (data) {
                var cpu = num(data.cpuUsage), mem = num(data.memoryUsage);
                var db = num(data.databaseSize), logDb = num(data.logDatabaseSize);
                if (cpu !== null) {
                    set('cpuUsage', cpu.toFixed(1) + '%');
                    var meter = document.getElementById('cpuMeter');
                    if (meter) { meter.style.width = Math.max(0, Math.min(100, cpu)) + '%'; }
                }
                if (mem !== null) { set('memoryUsage', mem.toFixed(0) + ' MB'); }
                if (db !== null) { set('databaseSize', db.toFixed(2).replace(/\.?0+$/, '') + ' MB'); }
                if (logDb !== null) { set('logDatabaseSize', logDb.toFixed(2).replace(/\.?0+$/, '') + ' MB'); }
                if (errorNote) { errorNote.hidden = true; }
            })
            .catch(function () {
                set('cpuUsage', '—');
                set('memoryUsage', '—');
                if (errorNote) { errorNote.hidden = false; }
            });
    }

    function start() { if (!timer) { refresh(); timer = window.setInterval(refresh, 10000); } }
    function stop() { if (timer) { window.clearInterval(timer); timer = null; } }

    document.addEventListener('visibilitychange', function () { if (document.hidden) { stop(); } else { start(); } });
    start();
})();
