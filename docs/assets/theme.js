// Theme toggle: inserts a toggle and persists preference in localStorage
(function(){
  function applyTheme(theme){
    if(theme==='dark') document.documentElement.classList.add('dark-theme');
    else document.documentElement.classList.remove('dark-theme');
  }

  function init(){
    const saved = localStorage.getItem('fc:theme');
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme = saved || (prefersDark ? 'dark' : 'light');
    applyTheme(theme);

    // Insert toggle in top header
    const header = document.querySelector('header') || document.querySelector('body');
    if(!header) return;

    const actions = document.createElement('div');
    actions.className = 'top-actions';

    const btn = document.createElement('button');
    btn.id = 'themeToggle';
    btn.className = 'btn';
    btn.type = 'button';
    btn.textContent = theme === 'dark' ? 'Dark' : 'Light';
    btn.addEventListener('click', function(){
      const current = document.documentElement.classList.contains('dark-theme') ? 'dark' : 'light';
      const next = current === 'dark' ? 'light' : 'dark';
      applyTheme(next);
      localStorage.setItem('fc:theme', next);
      btn.textContent = next === 'dark' ? 'Dark' : 'Light';
    });

    // Add a small accent button linking home
    const home = document.createElement('a');
    home.href = 'index.html';
    home.className = 'btn';
    home.textContent = 'Home';
    home.style.textDecoration = 'none';

    actions.appendChild(home);
    actions.appendChild(btn);

    // append to header
    header.appendChild(actions);
  }

  if(document.readyState==='loading') document.addEventListener('DOMContentLoaded', init); else init();
})();