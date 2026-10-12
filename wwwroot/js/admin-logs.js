(function () {
    const root = document.getElementById('adminLogsRoot');
    let currentPage = parseInt(root?.dataset?.currentPage || '1', 10);
    let totalPages = parseInt(root?.dataset?.totalPages || '1', 10);
    const PAGE_SIZE = 50;
    const searchInput = document.getElementById('searchInput');
    const searchBtn = document.getElementById('searchBtn');
    const prevBtn = document.getElementById('prevBtn');
    const nextBtn = document.getElementById('nextBtn');

    // Track errors to avoid console spam
    let lastErrorTime = 0;
    const ERROR_THROTTLE_MS = 5000;

    function sanitizeText(text) {
        const span = document.createElement('span');
        span.textContent = text;
        return span.innerHTML;
    }

    function createRowPair(log) {
        const tr = document.createElement('tr');
        // Level colours come from forest.css (fc-log--*) so they work in light and dark themes.
        tr.className = 'fc-log--' + String(getField(log, ['Level', 'level']) || 'information').toLowerCase();

        const ts = document.createElement('td'); ts.innerHTML = sanitizeText(getField(log, ['Timestamp','timestamp','TimeStamp']) || ''); tr.appendChild(ts);
        const lvl = document.createElement('td'); lvl.innerHTML = sanitizeText(getField(log, ['Level','level']) || ''); tr.appendChild(lvl);
        const ex = document.createElement('td'); ex.innerHTML = sanitizeText(getField(log, ['Exception','exception']) || ''); tr.appendChild(ex);
        const msg = document.createElement('td'); msg.innerHTML = sanitizeText(getField(log, ['Message','message','RenderedMessage','renderedMessage']) || ''); tr.appendChild(msg);

        const trDetail = document.createElement('tr');
        const tdDetail = document.createElement('td');
        tdDetail.setAttribute('colspan', '4');
        const pre = document.createElement('pre');
        pre.style.whiteSpace = 'pre-wrap';
        pre.style.wordBreak = 'break-word';
        pre.textContent = JSON.stringify(log, null, 2);
        tdDetail.appendChild(pre);
        trDetail.appendChild(tdDetail);
        trDetail.style.display = 'none';

        tr.addEventListener('click', function () {
            trDetail.style.display = trDetail.style.display === 'none' ? '' : 'none';
        });

        return { tr, trDetail };
    }

    function getField(obj, names){
                for(var i=0;i<names.length;i++){
                    if(obj[names[i]]!==undefined) return obj[names[i]];
                }
                return undefined;
            }

            function renderRows(logs) {
                const tbody = document.getElementById('logsTableBody');
                tbody.innerHTML = '';
                logs.forEach(function (log) {
                    const pair = createRowPair(log);
                    tbody.appendChild(pair.tr);
                    tbody.appendChild(pair.trDetail);
                });
            }

    async function loadPage(page) {
        const search = encodeURIComponent(searchInput.value || '');
        const url = `?handler=Logs&currentPage=${page}&searchQuery=${search}`;
        try {
            const res = await fetch(url, { cache: 'no-store' });
            if (!res.ok) return;
            const json = await res.json();
            renderRows(json.logs);
            currentPage = json.currentPage;
            totalPages = json.totalPages;
            document.getElementById('currentPage').textContent = currentPage;
            document.getElementById('totalPages').textContent = totalPages;

            prevBtn.disabled = currentPage <= 1;
            nextBtn.disabled = currentPage >= totalPages;
        } catch (e) {
            const now = Date.now();
            if (now - lastErrorTime > ERROR_THROTTLE_MS) {
                console.error('Failed to load logs', e);
                lastErrorTime = now;
            }
        }
    }

    function prependLog(log) {
        const tbody = document.getElementById('logsTableBody');
        const pair = createRowPair(log);
        if (tbody.firstChild) {
            tbody.insertBefore(pair.tr, tbody.firstChild);
            tbody.insertBefore(pair.trDetail, tbody.firstChild);
        } else {
            tbody.appendChild(pair.tr);
            tbody.appendChild(pair.trDetail);
        }
        const nodes = Array.from(tbody.children);
        const maxRows = PAGE_SIZE * 2;
        while (nodes.length > maxRows) {
            tbody.removeChild(tbody.lastChild);
            nodes.pop();
        }
    }

    // expose for potential external use
    window.__adminLogs = { loadPage, prependLog };

    // Script loader (used by SignalR dynamic load if needed)
    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            // already present
            var existing = document.querySelector('script[src="' + src + '"]');
            if (existing) {
                if (existing.getAttribute('data-loaded') === 'true') return resolve();
                existing.addEventListener('load', function () { resolve(); });
                existing.addEventListener('error', function () { reject(new Error('Failed to load ' + src)); });
                return;
            }

            var s = document.createElement('script');
            s.src = src;
            s.async = true;
            s.onload = function () { s.setAttribute('data-loaded', 'true'); resolve(); };
            s.onerror = function () { reject(new Error('Failed to load ' + src)); };
            document.head.appendChild(s);
        });
    }

    // SSE fallback: connect to server-sent events endpoint and receive JSON payloads
    function startSSE() {
        try {
            var es = new EventSource('/logs/sse');
            var errorCount = 0;

            es.onmessage = function (e) {
                try {
                    var payload = JSON.parse(e.data);
                    if (payload) {
                        if (currentPage === 1) {
                            prependLog(payload);
                        } else {
                            console.info('New log available (not on first page)');
                        }
                    }
                } catch (err) {
                    console.warn('Failed to parse SSE message', err, e.data);
                }
            };

            es.onerror = function (err) {
                errorCount++;
                // Only log every 10 errors to avoid console spam
                if (errorCount % 10 === 1) {
                    console.warn('SSE connection error (attempt ' + errorCount + ')', err);
                }
                // EventSource will attempt to reconnect automatically; no further action needed here.
            };

            console.info('SSE connected to /logs/sse');
            return true;
        } catch (ex) {
            console.warn('SSE not available', ex);
            return false;
        }
    }

    async function startSignalR() {
        var signalRUrl = 'https://unpkg.com/@microsoft/signalr@7.0.8/dist/browser/signalr.min.js';
        try {
            if (!window.signalR && !window.SignalR) {
                // attempt to dynamically load the client
                await loadScript(signalRUrl);
            }
        } catch (e) {
            console.warn('Failed to load SignalR client dynamically:', e);
            return;
        }

        var lib = window.signalR || window.SignalR;
        if (!lib || !lib.HubConnectionBuilder) {
            console.warn('SignalR client not available after loading; real-time updates disabled.');
            return;
        }

        var connection = new lib.HubConnectionBuilder().withUrl('/logsHub').withAutomaticReconnect().build();

        connection.on('NewLog', function (payload) {
            if (payload) {
                if (currentPage === 1) {
                    prependLog(payload);
                } else {
                    console.info('New log available (not on first page)');
                }
            } else {
                loadPage(currentPage);
            }
        });

        try {
            await connection.start();
            console.info('SignalR connected to /logsHub');
        } catch (err) {
            console.error('SignalR start failed:', err);
        }
    }

    // Download logs as CSV
    const fromDateInput = document.getElementById('fromDate');
    const toDateInput = document.getElementById('toDate');
    const downloadBtn = document.getElementById('downloadBtn');

    async function downloadLogs() {
        const fromDate = fromDateInput.value;
        const toDate = toDateInput.value;

        if (!fromDate || !toDate) {
            alert('Please select both From and To dates');
            return;
        }

        const fromDateTime = new Date(fromDate);
        const toDateTime = new Date(toDate);

        if (fromDateTime > toDateTime) {
            alert('From date must be before To date');
            return;
        }

        try {
            downloadBtn.disabled = true;
            downloadBtn.dataset.label = downloadBtn.dataset.label || downloadBtn.textContent;
            downloadBtn.textContent = downloadBtn.getAttribute('data-busy') || 'Downloading…';

            const form = new FormData();
            form.append('fromDate', fromDate);
            form.append('toDate', toDate);
            form.append('searchQuery', searchInput.value || '');

            // Get antiforgery token from the hidden input field
            const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
            if (tokenInput) {
                form.append('__RequestVerificationToken', tokenInput.value);
            }

            const response = await fetch('?handler=Download', {
                method: 'POST',
                body: form
            });

            if (!response.ok) {
                const errorText = await response.text();
                alert('Failed to download logs: ' + errorText);
                return;
            }

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'logs_' + new Date().toISOString().replace(/[:.]/g, '-') + '.csv';
            document.body.appendChild(a);
            a.click();
            window.URL.revokeObjectURL(url);
            document.body.removeChild(a);
        } catch (e) {
            console.error('Failed to download logs', e);
            alert('An error occurred while downloading logs: ' + e.message);
        } finally {
            downloadBtn.disabled = false;
            downloadBtn.textContent = downloadBtn.dataset.label || 'Download logs';
        }
    }

    downloadBtn.addEventListener('click', downloadLogs);

    // Set default date range (last 7 days)
    function setDefaultDateRange() {
        const now = new Date();
        const sevenDaysAgo = new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000);

        const formatDateTime = (date) => {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const hours = String(date.getHours()).padStart(2, '0');
            const minutes = String(date.getMinutes()).padStart(2, '0');
            return `${year}-${month}-${day}T${hours}:${minutes}`;
        };

        toDateInput.value = formatDateTime(now);
        fromDateInput.value = formatDateTime(sevenDaysAgo);
    }

    setDefaultDateRange();

    // Attach UI events
    searchBtn.addEventListener('click', function () { currentPage = 1; loadPage(currentPage); });
    searchInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); currentPage = 1; loadPage(currentPage); } });

    prevBtn.addEventListener('click', function () { if (currentPage > 1) { currentPage--; loadPage(currentPage); } });
    nextBtn.addEventListener('click', function () { if (currentPage < totalPages) { currentPage++; loadPage(currentPage); } });

    // initial load: prefer SSE (needs no extra client script). If SSE isn't available, try SignalR client.
    loadPage(currentPage).then(() => {
        var sseStarted = false;
        if (window.EventSource) {
            try {
                sseStarted = startSSE();
            } catch { sseStarted = false; }
        }

        // If SSE didn't start, attempt SignalR (useful if local signalr client is present)
        if (!sseStarted) {
            startSignalR();
        }
    });

    // Poll for updates as a fallback (every 3s) - will refresh current page when on page 1
    setInterval(function () {
        if (currentPage === 1) {
            loadPage(1);
        }
    }, 3000);
})();
