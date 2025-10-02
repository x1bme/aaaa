import axios from 'axios';
import { generatePkceChallenge } from 'src/utils/pkceHelper';

const AUTH_TOKEN_KEY = 'auth_token';
const REFRESH_TOKEN_KEY = 'refresh_token';

const apiClient = axios.create({
    baseURL: process.env.BACKEND_API_URL || '',
});

apiClient.interceptors.request.use( config => {
    const token = sessionStorage.getItem(AUTH_TOKEN_KEY);
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

apiClient.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error.config;
        if (error.response && error.response.status === 401 && !originalRequest._retry) {
            originalRequest._retry = true;
        try {
            const refreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY);
            if (!refreshToken) {
                redirectToLogin();
                return Promise.reject(error);
            }
            const newToken = await refreshAccessToken(refreshToken);
            if (newToken) {
                sessionStorage.setItem(AUTH_TOKEN_KEY, newToken);
                originalRequest.headers.Authorization = `Bearer ${newToken}`;
                return apiClient(originalRequest);
            }
        } catch(err) {
            redirectToLogin();
            return Promise.reject(err);
        }
    }
    return Promise.reject(error);
    }
);

export async function initiateLogin() {
    const clientId = 'frontend';
    const redirectUri = encodeURIComponent(window.location.origin + '/callback');
    const scope = 'api offline_access';
    const responseType = 'code';

    const {codeVerifier, codeChallenge} = await generatePkceChallenge();

    sessionStorage.setItem('code_verifier', codeVerifier);
    const currentPath = window.location.pathname;
    if (currentPath !== '/login' && currentPath !== '/callback') {
        sessionStorage.setItem('auth_redirect', currentPath)
    }

    window.location.href = '/connect/authorize?' +
    `client_id=${clientId}&` +
    `redirect_uri=${redirectUri}&` +
    `response_type=${responseType}&` +
    `scope=${scope}&` +
    `code_challenge=${codeChallenge}&` + 
    `code_challenge_method=S256`;
}

export async function handleCallback(code) {
    try {
        const clientId = 'frontend';
        const redirectUri = `${window.location.origin}/callback`;
        const codeVerifier = sessionStorage.getItem('code_verifier');

        if (!codeVerifier) {
            throw new Error('No code verifier found');
        }

        const response = await apiClient.post('/connect/token',
            new URLSearchParams({
                client_id: clientId,
                client_secret: 'frontend-secret',
                grant_type: 'authorization_code',
                code: code,
                redirect_uri: redirectUri,
                code_verifier: codeVerifier
            }),
            {
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            }
        );
        sessionStorage.setItem(AUTH_TOKEN_KEY, response.data.access_token);
        sessionStorage.setItem(REFRESH_TOKEN_KEY, response.data.refresh_token);
        sessionStorage.removeItem('code_verifier');
        try {
            await axios.get('/connect/clear-cookies')
        } catch (cookieErr) {
            console.warn('Could not clear session cookies:', cookieErr)
        }
        return {success: true};
    } catch(err) {
        console.error('Error during callback handling', err);
        return {success: false, error: err.message};
    }
}

async function refreshAccessToken(refreshToken) {
    try {
        const response = await authClient.post('/connect/token',
            new URLSearchParams({
                client_id: 'frontend',
                client_secret: 'frontend-secret',
                grant_type: 'refresh_token',
                refresh_token: refreshToken
            }),
            {
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded'
                }
            }
        );
        if (response.data.refresh_token) {
            sessionStorage.setItem(REFRESH_TOKEN_KEY, response.data.refresh_token);
        }
        return response.data.access_token;
    } catch(err) {
        console.error('Error refreshing token', err)
        throw err;
    }
}

function redirectToLogin() {
    sessionStorage.removeItem(AUTH_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    initiateLogin();
}

export function isAuthenticated() {
    return !!sessionStorage.getItem(AUTH_TOKEN_KEY);
}

export function logout() {
    sessionStorage.removeItem(AUTH_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    window.location.href = '/';
}

export function getApiClient() {
    return apiClient;
}