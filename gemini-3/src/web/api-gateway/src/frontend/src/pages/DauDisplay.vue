<template>
    <div class="dau-display q-pa-md">
        <div class="row justify-between items-center q-md-md">
            <h4 class="q-ma-none">DAU Management</h4>
            <q-btn
                color="primary"
                icon="add"
                label="New DAU"
                @click="openCreateForm"
            />
        </div>
        <div v-if="isLoading" class="row justify-center q-pa-lg">
            <q-spinner color="primary" size="3em"/>
        </div>
        <div v-else-if="!allDaus || allDaus.length === 0" class="text-center q-pa-lg">
            <p>No DAUs found. Create a new DAU to get started.</p>
        </div>
        <div v-else class="row q-col-gutter-md">
            <div v-for="dau in allDaus"
                :key="dau.id"
                class="col-xs-12 col-sm-6 col-md-4"
            >
                <q-card class="dau-card">
                    <q-card-section>
                        <div class="row justify-between items-center">
                            <div class="text-h6">
                                {{ dau.name || `DAU ${dau.serialNumber}` }}
                                <q-badge :color="getDauStatusColor(dau.status)">
                                    {{ mapStatusEnum(dau.status) }}
                                </q-badge>
                                <q-badge color="blue" v-if="dau.valveId">Attached</q-badge>
                                <q-badge color="grey" v-else>Available</q-badge>
                            </div>
                            <div>
                                <q-btn flat round color="primary" icon="build" @click="editDau(dau)"/>
                                <q-btn flat round color="negative" icon="delete" @click="confirmDelete(dau)" 
                                       :disable="dau.valveId !== null && dau.valveId !== 0"/>
                            </div>
                        </div>
                        <div class="text-subtitle2 q-mt-sm">
                            Location: {{ dau.location || 'Not specified' }}
                        </div>
                    </q-card-section>
                    <q-separator />
                    <q-card-section>
                        <q-list dense>
                            <q-item>
                                <q-item-section>Serial Number:</q-item-section>
                                <q-item-section>{{ dau.serialNumber || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>IP Address:</q-item-section>
                                <q-item-section>{{ dau.dauIPAddress || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>TCP Port:</q-item-section>
                                <q-item-section>{{ dau.tcpPort || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>UDP Port:</q-item-section>
                                <q-item-section>{{ dau.udpPort || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>Last Heartbeat:</q-item-section>
                                <q-item-section>{{ formatDate(dau.lastHeartbeat) }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>Last Calibration:</q-item-section>
                                <q-item-section>{{ formatDate(dau.lastCalibration) }}</q-item-section>
                            </q-item>

                            <q-item>
                                <q-item-section>Next Calibration:</q-item-section>
                                <q-item-section>{{ formatDate(dau.nextCalibration) }}</q-item-section>
                            </q-item>

                            <q-item v-if="dau.valveId">
                                <q-item-section>Attached to valve:</q-item-section>
                                <q-item-section>ID: {{ dau.valveId }}</q-item-section>
                            </q-item>
                        </q-list>
                    </q-card-section>
                </q-card>
            </div>
        </div>
        <q-dialog v-model="editDialogOpen" persistent>
            <dau-edit-form
                v-if="!isCreating && selectedDau"
                :dau="selectedDau"
                @saved="onFormSaved"
                @deleted="onDauDeleted"
            />
            <dau-create-form
                v-if="isCreating"
                @saved="onFormSaved"
            />
        </q-dialog>

        <q-dialog v-model="deleteConfirmOpen" persistent>
            <q-card>
                <q-card-section class="row items-center">
                    <q-avatar icon="warning" color="negative" text-color="white" />
                    <span class="q-ml-sm text-h6">Delete DAU</span>
                </q-card-section>
                <q-card-section>
                    Are you sure you want to delete DAU "{{ selectedDau?.serialNumber }}"?
                    <p class="text-caption q-mt-sm text-negative">
                        This action cannot be undone.
                    </p>
                </q-card-section>
                <q-card-actions align="right">
                    <q-btn flat label="Cancel" color="primary" v-close-popup />
                    <q-btn flat label="Delete" color="negative" :loading="loading" @click="deleteDau" />
                </q-card-actions>
            </q-card>
        </q-dialog>
    </div>
</template>

<script>
import { defineComponent, ref, computed, onMounted } from 'vue';
import { useDauStore } from 'src/stores/dauStore';
import { date } from 'quasar';
import DauEditForm from 'src/components/DauEditForm.vue';
import CreateDauForm from 'src/components/CreateDauForm.vue';

export default defineComponent({
    name: 'DAUDisplay',
    components: {
        'dau-edit-form': DauEditForm,
        'dau-create-form': CreateDauForm
    },
    setup() {
        const dauStore = useDauStore();
        const editDialogOpen = ref(false);
        const deleteConfirmOpen = ref(false);
        const isCreating = ref(false);
        const selectedDau = ref(null);
        const loading = ref(false);

        function formatDate(dateStr) {
            if (!dateStr) return 'N/A';
            try {
                return date.formatDate(new Date(dateStr), 'MM-DD-YYYY HH:mm');
            } catch (error) {
                return 'Invalid Date';
            }
        }

        function getDauStatusColor(status) {
            const statusValue = mapStatusEnum(status);
            const dauColors = {
                'Operational': 'positive',
                'NeedsCalibration': 'info',
                'NeedsMaintenance': 'warning',
                'Offline': 'negative',
            };
            return dauColors[statusValue] || 'grey';
        }

        function mapStatusEnum(enumVal) {
            const statusMapping = {
                0: 'Operational',
                1: 'NeedsCalibration',
                2: 'NeedsMaintenance',
                3: 'Offline'
            };
            return statusMapping[enumVal] || 'Unknown';
        }

        function openCreateForm() {
            isCreating.value = true;
            editDialogOpen.value = true;
        }

        function editDau(dau) {
            isCreating.value = false;
            selectedDau.value = dau;
            editDialogOpen.value = true;
        }

        function confirmDelete(dau) {
            selectedDau.value = dau;
            deleteConfirmOpen.value = true;
        }

        async function deleteDau() {
            try {
                loading.value = true;
                await dauStore.deleteDau(selectedDau.value.id);
                deleteConfirmOpen.value = false;
                await dauStore.fetchDaus();
            } catch (error) {
                console.error('Failed to delete DAU:', error);
            } finally {
                loading.value = false;
            }
        }

        function onFormSaved() {
            editDialogOpen.value = false;
            dauStore.fetchDaus();
        }

        function onDauDeleted() {
            editDialogOpen.value = false;
            dauStore.fetchDaus();
        }

        onMounted(async () => {
            try {
                await dauStore.fetchDaus();
            } catch (error) {
                console.error('Failed to fetch DAUs:', error);
            }
        });

        return {
            allDaus: computed(() => dauStore.allDaus),
            isLoading: computed(() => dauStore.isLoading),
            editDialogOpen,
            deleteConfirmOpen,
            isCreating,
            selectedDau,
            loading,
            formatDate,
            getDauStatusColor,
            mapStatusEnum,
            openCreateForm,
            editDau,
            confirmDelete,
            deleteDau,
            onFormSaved,
            onDauDeleted
        };
    }
});
</script>

<style scoped>
.dau-card {
    height: 100%;
}
</style>