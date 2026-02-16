// Auto-detect base path for IIS subpath deployment
const pathMatch = window.location.pathname.match(/^(.*?)\/Pages\//);
export const BASE_PATH = pathMatch ? pathMatch[1] : '';
