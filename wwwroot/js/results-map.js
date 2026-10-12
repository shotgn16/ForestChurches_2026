// Plots search results on a Leaflet map. Data comes from data-* attributes, so no inline script is needed (CSP).
(function () {
    'use strict';
    document.addEventListener('DOMContentLoaded', function () {
        var el = document.getElementById('fc-results-map');
        if (!el || typeof L === 'undefined') { return; }
        var points = [];
        try { points = JSON.parse(el.getAttribute('data-points') || '[]'); } catch (e) { return; }
        if (!points.length) { return; }

        var map = L.map(el, { scrollWheelZoom: false });
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap contributors',
            maxZoom: 18
        }).addTo(map);

        var bounds = [];
        points.forEach(function (p, i) {
            // Vector markers: Leaflet's default PNG icons are blocked by the site's image CSP.
            L.circleMarker([p.lat, p.lng], { radius: 10, color: (getComputedStyle(document.documentElement).getPropertyValue('--fc-surface').trim() || '#ffffff'), weight: 3, fillColor: (getComputedStyle(document.documentElement).getPropertyValue('--fc-primary').trim() || '#1F3D2B'), fillOpacity: 1 })
                .addTo(map)
                .bindTooltip((i + 1) + '. ' + p.name, { direction: 'top', offset: [0, -8] });
            bounds.push([p.lat, p.lng]);
        });

        var home = el.getAttribute('data-home');
        if (home) {
            try {
                var h = JSON.parse(home);
                L.circleMarker([h.lat, h.lng], { radius: 7, color: '#ffffff', weight: 2, fillColor: '#8A5A3B', fillOpacity: 1 }).addTo(map);
                bounds.push([h.lat, h.lng]);
            } catch (e) { /* ignore */ }
        }

        if (bounds.length === 1) { map.setView(bounds[0], 14); } else { map.fitBounds(bounds, { padding: [30, 30] }); }
    });
})();
