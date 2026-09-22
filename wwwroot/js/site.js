// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    const navigationLinks = document.querySelectorAll(".dashboard-nav-link");

    const normalizePath = (path) => path.replace(/\/+$/, "") || "/";

    if (navigationLinks.length) {
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
    }

    document.querySelectorAll("[data-password-toggle]").forEach((toggle) => {
        const passwordInput = document.querySelector(toggle.dataset.passwordToggle);
        if (!passwordInput) {
            return;
        }

        toggle.addEventListener("click", () => {
            const isPasswordVisible = passwordInput.type === "text";
            passwordInput.type = isPasswordVisible ? "password" : "text";
            toggle.classList.toggle("is-visible", !isPasswordVisible);
            toggle.setAttribute("aria-pressed", String(!isPasswordVisible));
            toggle.setAttribute("aria-label", isPasswordVisible ? "Show password" : "Hide password");
        });
    });
})();
