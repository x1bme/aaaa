<template>
    <q-page padding class="flex flex-center">
        <q-card class="logout-card">
            <q-card-section class="text-center">
                <div class="text-h4 q-mb-md">Crane Nuclear</div>
                <div class="text-subtitle1 q-mb-xl">Are you sure you want to Log out?</div>
                <q-btn color="primary" label="Log Out" size="lg" @click="handleLogout" class="full-width"/>
            </q-card-section>
        </q-card>
    </q-page>
</template>

<script>
import { defineComponent } from 'vue'; 
import { logout } from 'src/services/authService';
export default defineComponent({
    name: 'LogoutPage',
    methods: {
        handleLogout() {
            logout();
        }
    }
});
</script>

<style scoped>
    .logout-card {
        max-width: 400px;
        width: 100%;
    }
</style>