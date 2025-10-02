import { boot } from "quasar/wrappers";
import { isAuthenticated, initiateLogin } from "src/services/authService";

export default boot(({ router }) => {
    router.beforeEach(async (to, from, next) => {
        if (to.matched.some(record => record.meta.requiresAuth)) {
            if (!isAuthenticated()) {
                sessionStorage.setItem('auth_redirect', to.fullPath);
                next('login')
                return
            }  
        }
        next()
    })
})
