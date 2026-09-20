// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const navigationLinks = document.querySelectorAll(".dashboard-nav-link");

    if (!navigationLinks.length) {
        return;
    }

    const normalizePath = (path) => path.replace(/\/+$/, "") || "/";

    const updateActiveLink = () => {
        const currentPath = normalizePath(window.location.pathname);

        navigationLinks.forEach((link) => {
            const linkUrl = new URL(link.href, window.location.origin);
            const isHashLink = linkUrl.pathname === currentPath && linkUrl.hash;
            const isCurrentPage = !linkUrl.hash
                && normalizePath(linkUrl.pathname) === currentPath;
            const isCurrentSection = isHashLink
                && linkUrl.hash === window.location.hash;

            link.classList.toggle("active", isCurrentPage || isCurrentSection);
        });
    };

    updateActiveLink();
    window.addEventListener("hashchange", updateActiveLink);
})();
