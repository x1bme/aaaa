<template>
    <div>
        <q-btn
            color="info"
            icon="history"
            label="Test History"
            @click="openDialog"
            size="sm"
        >
            <q-tooltip>View and download test history</q-tooltip>
        </q-btn>
        
        <q-dialog v-model="showDialog" @show="loadTests">
            <q-card style="min-width: 500px; max-width: 600px">
                <q-card-section>
                    <div class="text-h6">Test History - Valve {{ valveId }}</div>
                </q-card-section>
                
                <q-card-section v-if="loading">
                    <div class="row justify-center q-pa-md">
                        <q-spinner color="primary" size="3em" />
                    </div>
                    <div class="text-center">Loading tests...</div>
                </q-card-section>
                
                <q-card-section v-else-if="tests.length === 0">
                    <q-banner class="bg-grey-3">
                        <template v-slot:avatar>
                            <q-icon name="info" color="grey-7" />
                        </template>
                        No tests found for this valve.
                    </q-banner>
                </q-card-section>
                
                <q-card-section v-else>
                    <!-- Test Dropdown -->
                    <q-select
                        v-model="selectedTest"
                        :options="tests"
                        option-value="testId"
                        label="Select Test"
                        emit-value
                        map-options
                    >
                        <template v-slot:option="scope">
                            <q-item v-bind="scope.itemProps">
                                <q-item-section>
                                    <q-item-label>{{ formatDate(scope.opt.acquisitionDate) }}</q-item-label>
                                    <q-item-label caption>
                                        Test ID: {{ scope.opt.testId }} | Size: {{ scope.opt.blobSizeFormatted }}
                                    </q-item-label>
                                </q-item-section>
                                <q-item-section side>
                                    <q-badge :color="scope.opt.hasBlobData ? 'positive' : 'negative'">
                                        {{ scope.opt.hasBlobData ? 'Has Data' : 'No Data' }}
                                    </q-badge>
                                </q-item-section>
                            </q-item>
                        </template>
                        
                        <template v-slot:selected-item="scope">
                            <div>{{ formatDate(scope.opt.acquisitionDate) }}</div>
                        </template>
                    </q-select>
                    
                    <!-- Test Details Card (shown when test selected) -->
                    <q-card v-if="selectedTestData" flat bordered class="q-mt-md">
                        <q-card-section>
                            <div class="text-subtitle2 q-mb-sm">Test Details</div>
                            <q-list dense>
                                <q-item>
                                    <q-item-section>
                                        <q-item-label caption>Test ID</q-item-label>
                                        <q-item-label>{{ selectedTestData.testId }}</q-item-label>
                                    </q-item-section>
                                </q-item>
                                <q-item>
                                    <q-item-section>
                                        <q-item-label caption>Acquisition Date</q-item-label>
                                        <q-item-label>{{ formatDate(selectedTestData.acquisitionDate) }}</q-item-label>
                                    </q-item-section>
                                </q-item>
                                <q-item>
                                    <q-item-section>
                                        <q-item-label caption>File Size</q-item-label>
                                        <q-item-label>{{ selectedTestData.blobSizeFormatted }}</q-item-label>
                                    </q-item-section>
                                </q-item>
                                <q-item>
                                    <q-item-section>
                                        <q-item-label caption>Has Data</q-item-label>
                                        <q-item-label>
                                            <q-badge :color="selectedTestData.hasBlobData ? 'positive' : 'negative'">
                                                {{ selectedTestData.hasBlobData ? 'Yes' : 'No' }}
                                            </q-badge>
                                        </q-item-label>
                                    </q-item-section>
                                </q-item>
                            </q-list>
                        </q-card-section>
                        
                        <q-card-actions align="right">
                            <q-btn
                                color="primary"
                                icon="download"
                                label="Download Test"
                                @click="downloadTest"
                                :disable="!selectedTestData.hasBlobData || downloading"
                                :loading="downloading"
                            />
                        </q-card-actions>
                    </q-card>
                </q-card-section>
                
                <q-card-actions align="right">
                    <q-btn flat label="Close" color="primary" v-close-popup />
                </q-card-actions>
            </q-card>
        </q-dialog>
    </div>
</template>

<script>
import { ref, computed } from 'vue';
import dbService from 'src/services/dbService';
import { useQuasar, date as qDate } from 'quasar';

export default {
    name: 'TestHistoryDialog',
    props: {
        valveId: {
            type: Number,
            required: true
        }
    },
    setup(props) {
        const $q = useQuasar();
        const showDialog = ref(false);
        const loading = ref(false);
        const downloading = ref(false);
        const tests = ref([]);
        const selectedTest = ref(null);
        
        // Get the full test object when a test is selected
        const selectedTestData = computed(() => {
            if (!selectedTest.value) return null;
            return tests.value.find(t => t.testId === selectedTest.value);
        });
        
        function openDialog() {
            showDialog.value = true;
        }
        
        function formatDate(dateStr) {
            if (!dateStr) return 'N/A';
            try {
                return qDate.formatDate(new Date(dateStr), 'MMM DD, YYYY h:mm A');
            } catch (error) {
                return 'Invalid Date';
            }
        }
        
        async function loadTests() {
            try {
                loading.value = true;
                selectedTest.value = null;
                
                const response = await dbService.getTestsByValve(props.valveId);
                tests.value = response.data || [];
                
                if (tests.value.length === 0) {
                    $q.notify({
                        type: 'info',
                        message: 'No tests found for this valve',
                        icon: 'info'
                    });
                }
            } catch (error) {
                console.error('Error loading tests:', error);
                $q.notify({
                    type: 'negative',
                    message: 'Failed to load tests',
                    icon: 'error'
                });
                tests.value = [];
            } finally {
                loading.value = false;
            }
        }
        
        async function downloadTest() {
            if (!selectedTestData.value) return;
            
            try {
                downloading.value = true;
                
                const response = await dbService.downloadTest(selectedTestData.value.testId);
                
                // Create blob and download
                const blob = new Blob([response.data], { type: 'application/octet-stream' });
                const url = window.URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = `valve-${props.valveId}-test-${selectedTestData.value.testId}.vitda`;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                window.URL.revokeObjectURL(url);
                
                $q.notify({
                    type: 'positive',
                    message: 'Test downloaded successfully',
                    icon: 'download'
                });
            } catch (error) {
                console.error('Error downloading test:', error);
                $q.notify({
                    type: 'negative',
                    message: 'Failed to download test',
                    icon: 'error'
                });
            } finally {
                downloading.value = false;
            }
        }
        
        return {
            showDialog,
            loading,
            downloading,
            tests,
            selectedTest,
            selectedTestData,
            openDialog,
            formatDate,
            loadTests,
            downloadTest
        };
    }
};
</script>

<style scoped>
.q-item {
    min-height: 60px;
}
</style>
