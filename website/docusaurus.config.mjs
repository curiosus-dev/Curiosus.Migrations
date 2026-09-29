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
    return site;
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
    themeConfig: {
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
            links: [{
                title: 'Curiosus',
                items: [
                    { label: 'GitHub', href: `https://github.com/${owner}` },
                    { label: 'NuGet', href: nugetUrl },
                    { label: 'dotnet-tools', href: `https://github.com/${owner}/dotnet-tools` },
                ],
            }],
            copyright: `© ${new Date().getFullYear()} Curiosus contributors`,
        },
        prism: {
            theme: prismThemes.github,
            darkTheme: prismThemes.dracula,
            additionalLanguages: ['csharp', 'sql', 'bash', 'powershell', 'json'],
        },
    },
};
