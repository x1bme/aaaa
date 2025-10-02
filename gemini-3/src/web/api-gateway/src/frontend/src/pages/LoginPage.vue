<template>
    <q-page padding class="flex flex-center">
        <q-card class="login-card">
            <q-card-section class="text-center">
                <div class="text-h4 q-mb-md">Crane Nuclear</div>
                <div class="text-subtitle1 q-mb-xl">Please log in to access Gemini</div>
                <q-btn color="primary" label="Log In" size="lg" @click="handleLogin" class="full-width"/>
            </q-card-section>
        </q-card>
    </q-page>
</template>

<script>
import { defineComponent } from 'vue'; 
import { initiateLogin } from 'src/services/authService';
export default defineComponent({
    name: 'LoginPage',
    methods: {
        handleLogin() {
            initiateLogin();
        }
    }
});
</script>

<style scoped>
    .login-card {
        max-width: 400px;
        width: 100%;
    }
</style>