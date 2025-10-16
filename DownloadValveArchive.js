<template>
    <q-btn
        color="secondary"
        icon="archive"
        label="Download Archive"
        @click="downloadArchive"
        :loading="downloading"
        :disable="downloading"
        size="sm"
    >
        <q-tooltip>Download the latest test archive for this valve</q-tooltip>
    </q-btn>
</template>

<script>
import { ref } from 'vue';
import dbService from 'src/services/dbService';
import { useQuasar } from 'quasar';

export default {
    name: 'DownloadValveArchive',
    props: {
        valveId: {
            type: Number,
            required: true
        }
    },
    setup(props) {
        const $q = useQuasar();
        const downloading = ref(false);
        
        async function downloadArchive() {
            try {
                downloading.value = true;
                
                const response = await dbService.getValveArchive(props.valveId);
                
                // Create blob and download
                const blob = new Blob([response.data], { type: 'application/octet-stream' });
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `valve-${props.valveId}-archive-${new Date().toISOString().split('T')[0]}.vitda`;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.URL.revokeObjectURL(url);
                
                $q.notify({
                    type: 'positive',
                    message: 'Valve archive downloaded successfully',
                    icon: 'download'
                });
            } catch (error) {
                console.error('Error downloading valve archive:', error);
                
                const errorMessage = error.response?.status === 404
                    ? 'No archive found for this valve'
                    : 'Failed to download valve archive';
                
                $q.notify({
                    type: 'negative',
                    message: errorMessage,
                    icon: 'error'
                });
            } finally {
                downloading.value = false;
            }
        }
        
        return {
            downloading,
            downloadArchive
        };
    }
};
</script>
