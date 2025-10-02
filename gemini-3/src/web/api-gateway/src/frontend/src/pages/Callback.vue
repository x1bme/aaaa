<template>
    <q-page class="flex flex-center">
        <q-card v-if="loading" class="callback-card q-pa-lg text-center">
            <q-spinner-dots size="50px" color="primary" class="q-mb-md"/>
            <div class="text-h6">Login on the other page using your credentials</div>
        </q-card>
        <q-card v-if="error" class="callback-card q-pa-lg text-center">
            <q-icon name="error" color="negative" size="50px" class="q-mb-md" />
            <div class="q-mb-md">{{ error }}</div>
            <q-btn color="primary" label="Try Again" @click="tryAgain" icon="refresh" />
        </q-card>
    </q-page>
</template>

<script>
    import { handleCallback, initiateLogin } from 'src/services/authService';
    export default {
        name: 'Callback',
        data() {
            return {
                loading: true,
                error: null
            };
        },
        async created() {
            try {
                const code = this.$route.query.code;
                if (!code) {
                    this.error = "No authorization code found in the URL";
                    this.loading = false;
                    return;
                }
                const result = await handleCallback(code);
                if (result.success) {
                    const redirectPath = sessionStorage.getItem("auth_redirect") || '/';
                    sessionStorage.removeItem("auth_redirect");
                    this.$router.push(redirectPath);
                } else {
                    this.error = this.error || "Authentication failed";
                    this.loading = false;
                }
            } catch(err) {
                this.err = 'Error during authentication process'
                this.loading = false;
                console.error('Encountered an error while authenticating user: ', err);
            }
        },
        methods: {
            tryAgain() {
                initiateLogin();
            }
        }
    };
</script>

<style scoped>
    .callback-card {
        width: 100%;
        max-width: 400px;
    }
</style>