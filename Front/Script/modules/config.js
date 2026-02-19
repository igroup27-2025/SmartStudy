// Auto-detect base path for IIS subpath deployment
const pathMatch = window.location.pathname.match(/^(.*?)\/Pages\//);
export const BASE_PATH = pathMatch ? pathMatch[1] : '';

// API lives on a separate IIS app (tar1 instead of tar2)
// In production, replace the last path segment; in dev, same as BASE_PATH
export const API_BASE_PATH = BASE_PATH.replace(/\/tar2$/, '/tar1');
