// Synced from https://github.com/curiosus-dev/dotnet-tools, do not edit in place.
// Repository specifics come from docs.json in the repository root, content from docs/ (symlinks included).
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, realpathSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { themes as prismThemes } from 'prism-react-renderer';

const website = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.dirname(website);
const docsDir = path.join(repoRoot, 'docs');

function loadSite() {
    const file = path.join(repoRoot, 'docs.json');
    if (!existsSync(file)) throw new Error(`docs.json: not found in ${repoRoot}`);
    const site = JSON.parse(readFileSync(file, 'utf8'));
    for (const key of ['title', 'tagline']) {
        if (typeof site[key] !== 'string' || !site[key]) throw new Error(`docs.json: "${key}" is required`);
    }
    const { siteVerification, ...docSearch } = site.algolia ?? {};
    for (const key of Object.keys(docSearch).length ? ['appId', 'apiKey', 'indexName'] : []) {
        if (typeof docSearch[key] !== 'string' || !docSearch[key]) {
            throw new Error(`docs.json: "algolia.${key}" is required for Algolia DocSearch`);
        }
    }
    return { ...site, siteVerification, docSearch: Object.keys(docSearch).length ? docSearch : undefined };
}

function resolveRepository() {
    const value = process.env.GITHUB_REPOSITORY
        || execFileSync('git', ['-C', repoRoot, 'remote', 'get-url', 'origin'], { encoding: 'utf8' });
    const match = /^(?:(?:git@|https:\/\/)github\.com[:/])?([\w.-]+)\/([\w.-]+?)(?:\.git)?\/?$/.exec(value.trim());
    if (!match) throw new Error(`Cannot parse a GitHub repository from "${value.trim()}"`);
    return { owner: match[1], name: match[2] };
}

const site = loadSite();
const { owner, name } = resolveRepository();
const repositoryUrl = `https://github.com/${owner}/${name}`;
const nugetUrl = 'https://www.nuget.org/profiles/curiosus-dev';
const realRoot = realpathSync(repoRoot);

// Symlinked pages (READMEs, CHANGELOGs) are edited where the symlink points to.
function editUrl({ docPath }) {
    const file = realpathSync(path.join(docsDir, docPath));
    return `${repositoryUrl}/edit/main/${path.relative(realRoot, file).split(path.sep).join('/')}`;
}

// The home page is usually a symlinked README, which cannot have sidebar_position: keep it first.
async function sidebarItemsGenerator({ defaultSidebarItemsGenerator, ...args }) {
    const items = await defaultSidebarItemsGenerator(args);
    const home = items.findIndex((item) => item.type === 'doc' && item.id === 'index');
    return home > 0 ? [items[home], ...items.filter((_, i) => i !== home)] : items;
}

// Cross links in the footer; a library with a documentation site links to it, the others to the repository.
const libraries = [
    { name: 'Curiosus.Migrations', site: true },
    { name: 'Curiosus.Utils', site: true },
    { name: 'Curiosus.TelegramBot', site: false },
];
const libraryLinks = libraries
    .filter((library) => library.name !== name)
    .map((library) => ({
        label: library.name,
        href: library.site ? `https://${owner}.github.io/${library.name}/` : `https://github.com/${owner}/${library.name}`,
    }));

// Algolia DocSearch when docs.json has its keys (https://docsearch.algolia.com), a local search index otherwise.
// algolia.siteVerification alone only adds the meta tag Algolia checks to verify the domain.
const localSearch = [
    '@easyops-cn/docusaurus-search-local',
    { hashed: true, indexBlog: false, docsDir: docsDir, docsRouteBasePath: '/', highlightSearchTermsOnTargetPage: true },
];

export default {
    title: site.title,
    tagline: site.tagline,
    favicon: 'img/curiosus-logo-128.png',
    url: `https://${owner}.github.io`,
    baseUrl: `/${name}/`,
    organizationName: owner,
    projectName: name,
    trailingSlash: false,
    onBrokenLinks: 'throw',
    onBrokenAnchors: 'warn',
    markdown: { hooks: { onBrokenMarkdownLinks: 'throw' } },
    i18n: { defaultLocale: 'en', locales: ['en'] },
    // Webpack resolves symlinks to their targets outside docs/, where the docs plugin does not find the page metadata.
    plugins: [() => ({ name: 'keep-symlinks', configureWebpack: () => ({ resolve: { symlinks: false } }) })],
    presets: [[
        'classic',
        {
            docs: { path: docsDir, routeBasePath: '/', sidebarPath: './sidebars.mjs', sidebarItemsGenerator, editUrl },
            blog: false,
            theme: { customCss: './src/css/custom.css' },
        },
    ]],
    headTags: site.siteVerification
        ? [{ tagName: 'meta', attributes: { name: 'algolia-site-verification', content: site.siteVerification } }]
        : [],
    themes: site.docSearch ? [] : [localSearch],
    themeConfig: {
        ...(site.docSearch && { algolia: { ...site.docSearch, contextualSearch: true } }),
        colorMode: { respectPrefersColorScheme: true },
        navbar: {
            title: site.title,
            logo: { alt: 'Curiosus', src: 'img/curiosus-logo.svg' },
            items: [
                { href: nugetUrl, label: 'NuGet', position: 'right' },
                { href: repositoryUrl, label: 'GitHub', position: 'right' },
            ],
        },
        footer: {
            style: 'dark',
            links: [
                { title: 'Libraries', items: libraryLinks },
                {
                    title: 'Curiosus',
                    items: [
                        { label: 'GitHub', href: `https://github.com/${owner}` },
                        { label: 'NuGet', href: nugetUrl },
                    ],
                },
            ],
            copyright: `© ${new Date().getFullYear()} Curiosus contributors`,
        },
        prism: {
            theme: prismThemes.github,
            darkTheme: prismThemes.dracula,
            additionalLanguages: ['csharp', 'sql', 'bash', 'powershell', 'json'],
        },
    },
};
