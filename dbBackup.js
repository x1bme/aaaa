<template>
    <q-btn
        color="primary"
        icon="backup"
        label="Backup Database"
        @click="downloadBackup"
        :loading="downloading"
        :disable="downloading"
    >
        <q-tooltip>Download a backup of the entire database</q-tooltip>
    </q-btn>
</template>

<script>
import { ref } from 'vue';
import dbService from 'src/services/dbService';
import { useQuasar } from 'quasar';

export default {
    name: 'DatabaseBackup',
    setup() {
        const $q = useQuasar();
        const downloading = ref(false);
        
        async function downloadBackup() {
            try {
                downloading.value = true;
                
                const response = await dbService.getDatabaseBackup();
                
                // Create blob and download
                const blob = new Blob([response.data], { type: 'application/sql' });
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `database_backup_${new Date().toISOString().split('T')[0]}.sql`;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.URL.revokeObjectURL(url);
                
                $q.notify({
                    type: 'positive',
                    message: 'Database backup downloaded successfully',
                    icon: 'backup'
                });
            } catch (error) {
                console.error('Error downloading database backup:', error);
                $q.notify({
                    type: 'negative',
                    message: 'Failed to download database backup',
                    icon: 'error'
                });
            } finally {
                downloading.value = false;
            }
        }
        
        return {
            downloading,
            downloadBackup
        };
    }
};
</script>
