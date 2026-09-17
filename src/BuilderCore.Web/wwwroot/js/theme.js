window.builderCoreTheme = {
  init: function () {
    const stored = localStorage.getItem('theme');
    const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    const theme = stored ?? (prefersDark ? 'dark' : 'light');
    document.documentElement.classList.toggle('dark', theme === 'dark');
  },
  toggle: function () {
    const isDark = document.documentElement.classList.toggle('dark');
    localStorage.setItem('theme', isDark ? 'dark' : 'light');
    return isDark ? 'dark' : 'light';
  },
  isDark: function () {
    return document.documentElement.classList.contains('dark');
  }
};

window.builderCoreTheme.init();
