// Forest Churches – help pages (/docs).
// Builds the "On this page" list from the article's h2 headings and highlights the section in view.
// Kept as an external file (no inline script) so it works with a Content-Security-Policy.
(function () {
    'use strict';

    function slug(text) {
        return text.toLowerCase().trim()
            .replace(/[^\w\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .substring(0, 60);
    }

    document.addEventListener('DOMContentLoaded', function () {
        var article = document.querySelector('.docs-article');
        var toc = document.querySelector('[data-docs-toc]');
        if (!article) { return; }

        // Give every h2/h3 an id so links like /docs/churches#password-rules work.
        var used = {};
        Array.prototype.slice.call(article.querySelectorAll('h2, h3')).forEach(function (h) {
            if (!h.id) {
                var id = slug(h.textContent || 'section') || 'section';
                while (used[id]) { id += '-x'; }
                h.id = id;
            }
            used[h.id] = true;
        });
        var headings = Array.prototype.slice.call(article.querySelectorAll('h2'));

        // Ids are added after the page loads, so jump to the requested section ourselves.
        if (window.location.hash.length > 1) {
            var target = document.getElementById(decodeURIComponent(window.location.hash.substring(1)));
            if (target) { target.scrollIntoView(); }
        }

        if (!toc || headings.length < 2) {
            if (toc) { toc.hidden = true; }
            return;
        }

        var list = document.createElement('ol');
        var links = headings.map(function (h) {
            var li = document.createElement('li');
            var a = document.createElement('a');
            a.href = '#' + h.id;
            a.textContent = h.textContent;
            li.appendChild(a);
            list.appendChild(li);
            return a;
        });
        toc.appendChild(list);

        if (!('IntersectionObserver' in window)) { return; }
        var current = null;
        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    var index = headings.indexOf(entry.target);
                    if (current) { current.classList.remove('is-active'); }
                    current = links[index];
                    if (current) { current.classList.add('is-active'); }
                }
            });
        }, { rootMargin: '0px 0px -70% 0px', threshold: 0 });
        headings.forEach(function (h) { observer.observe(h); });
    });
})();
