
<template>
<div>
    <q-btn color="primary" 
    :label="isDownloading ? 'Downloading...' : (isCollecting ? 'Stop Collection': 'Start Collection')"
    :color="isCollecting ? 'negative' : 'positive'"
    :icon="isCollecting ? 'stop' : 'play_arrow'"
    @click="toggleCollection"
    size="md"
    >
        <q-circular-progress
        v-if="isCollecting"
        indeterminate
        rounded
        color="white"
        class="q-pl-xs"
        />
    </q-btn>
</div>
</template>

<script>
import {ref} from 'vue';
import dbService from 'src/services/dbService';
export default {
    name: 'CollectionToggle',
    setup() {
        const isDownloading = ref(false)
        const isCollecting = ref(false)
        const downloadFile = async () => {
            //sample data
            const data = await dbService.getValveArchive(1)
        }
        const toggleCollection = async () => {
            if (isCollecting.value) {
                isDownloading.value = true
                downloadFile()
                isDownloading.value = false
                isCollecting.value = false
            } else {
                isCollecting.value = true
            }
        }
        return {
            isCollecting,
            isDownloading,
            toggleCollection
        }
    }
}
</script>