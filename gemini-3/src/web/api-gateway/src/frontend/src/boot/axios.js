import { boot } from 'quasar/wrappers';
import axios from 'axios';

const AUTH_TOKEN_KEY = 'auth_token';

const api = axios.create({
    baseURL: process.env.BACKEND_API_URL || '',
});

const db = axios.create({
    baseURL: process.env.BACKEND_DB_URL || '',
});

api.interceptors.request.use( config => {
    const token = sessionStorage.getItem(AUTH_TOKEN_KEY);
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
}, (error) => {
    return Promise.reject(error);
});

api.interceptors.response.use(
    (response) => {
        return response;
    },
    (error) => {
        if (error.response && error.response.status == 401) {
            sessionStorage.removeItem(AUTH_TOKEN_KEY);
            window.location.href = '/login';
        }
        return Promise.reject(error);
    }
);

export default boot(({app}) => {
    app.config.globalProperties.$axios = axios;
    app.config.globalProperties.$api = api;
});

export {api, db};