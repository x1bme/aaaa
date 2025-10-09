<template>
    <div class="valve-display q-pa-md">
        <div class="row justify-between items-center q-mb-md">
            <h4 class="q-ma-none">Valve Management</h4>
            <q-btn
                color="primary"
                icon="add"
                label="New Valve"
                @click="openCreateForm"
            />
        </div>
        
        <div v-if="isLoading" class="row justify-center q-pa-lg">
            <q-spinner color="primary" size="3em"/>
        </div>
        
        <div v-else-if="!allValves || allValves.length === 0" class="text-center q-pa-lg">
            <q-icon name="info" size="64px" color="grey-5" class="q-mb-md" />
            <p class="text-h6">No valves found</p>
            <p class="text-body2 text-grey-7">Create a new valve to get started.</p>
        </div>
        
        <div v-else class="row q-col-gutter-md">
            <div v-for="valve in allValves"
                :key="valve.id"
                class="col-xs-12 col-sm-6 col-md-4"
            >
                <q-card class="valve-card">
                    <q-card-section>
                        <div class="row justify-between items-center">
                            <div class="text-h6">
                                {{ valve.name }}
                                <q-badge :color="getValveStatusColor(valve)">
                                    {{ getValveStatus(valve) }}
                                </q-badge>
                            </div>
                            <div>
                                <q-btn flat round color="grey" icon="history" @click="showLogs(valve)">
                                    <q-tooltip>View Logs</q-tooltip>
                                </q-btn>
                                <q-btn flat round color="primary" icon="build" @click="editValve(valve)">
                                    <q-tooltip>Edit Valve</q-tooltip>
                                </q-btn>
                            </div>
                        </div>
                        <div class="text-subtitle2 q-mt-sm text-grey-7">
                            <q-icon name="place" size="xs" /> {{ valve.location || 'Not specified' }}
                        </div>
                        <div class="text-caption text-grey-6">
                            Installed: {{ formatDate(valve.installationDate) }}
                        </div>
                    </q-card-section>
                    
                    <q-separator />
                    
                    <!-- ATV DAU Section -->
                    <q-card-section>
                        <div class="text-subtitle2 q-mb-sm">
                            <q-icon name="router" size="xs" class="q-mr-xs" />
                            ATV DAU
                        </div>
                        <q-list dense v-if="valve.atv">
                            <q-item>
                                <q-item-section>
                                    <q-item-label>
                                        <strong>{{ valve.atv.dauTag || valve.atv.dauId }}</strong>
                                        <q-badge :color="getDauStatusColor(valve.atv.status)" class="q-ml-xs">
                                            {{ mapStatusEnum(valve.atv.status) }}
                                        </q-badge>
                                    </q-item-label>
                                    <q-item-label caption>{{ valve.atv.dauIPAddress }}</q-item-label>
                                </q-item-section>
                            </q-item>
                            <q-item>
                                <q-item-section>
                                    <q-item-label caption>Last Heartbeat</q-item-label>
                                    <q-item-label>{{ formatDate(valve.atv.lastHeartbeat) }}</q-item-label>
                                </q-item-section>
                            </q-item>
                        </q-list>
                        <div v-else class="text-negative">
                            <q-icon name="warning" />
                            No ATV DAU attached
                        </div>
                    </q-card-section>

                    <q-separator />

                    <!-- Remote DAU Section -->
                    <q-card-section>
                        <div class="text-subtitle2 q-mb-sm">
                            <q-icon name="router" size="xs" class="q-mr-xs" />
                            Remote DAU
                        </div>
                        <q-list dense v-if="valve.remote">
                            <q-item>
                                <q-item-section>
                                    <q-item-label>
                                        <strong>{{ valve.remote.dauTag || valve.remote.dauId }}</strong>
                                        <q-badge :color="getDauStatusColor(valve.remote.status)" class="q-ml-xs">
                                            {{ mapStatusEnum(valve.remote.status) }}
                                        </q-badge>
                                    </q-item-label>
                                    <q-item-label caption>{{ valve.remote.dauIPAddress }}</q-item-label>
                                </q-item-section>
                            </q-item>
                            <q-item>
                                <q-item-section>
                                    <q-item-label caption>Last Heartbeat</q-item-label>
                                    <q-item-label>{{ formatDate(valve.remote.lastHeartbeat) }}</q-item-label>
                                </q-item-section>
                            </q-item>
                        </q-list>
                        <div v-else class="text-negative">
                            <q-icon name="warning" />
                            No Remote DAU attached
                        </div>
                    </q-card-section>
                    
                    <q-separator />
                    
                    <q-card-section>
                        <data-collection />
                    </q-card-section>
                </q-card>
            </div>
        </div>
        
        <!-- Summary Cards -->
        <div v-if="allValves && allValves.length > 0" class="row q-col-gutter-md q-mt-md">
            <div class="col-xs-12 col-sm-6 col-md-3">
                <q-card class="bg-positive text-white">
                    <q-card-section>
                        <div class="text-h6">{{ activeValves.length }}</div>
                        <div class="text-caption">Active Valves</div>
                    </q-card-section>
                </q-card>
            </div>
            <div class="col-xs-12 col-sm-6 col-md-3">
                <q-card class="bg-warning text-white">
                    <q-card-section>
                        <div class="text-h6">{{ valvesNeedingAttention.length }}</div>
                        <div class="text-caption">Need Attention</div>
                    </q-card-section>
                </q-card>
            </div>
            <div class="col-xs-12 col-sm-6 col-md-3">
                <q-card class="bg-negative text-white">
                    <q-card-section>
                        <div class="text-h6">{{ offlineValves.length }}</div>
                        <div class="text-caption">Offline</div>
                    </q-card-section>
                </q-card>
            </div>
            <div class="col-xs-12 col-sm-6 col-md-3">
                <q-card class="bg-grey-7 text-white">
                    <q-card-section>
                        <div class="text-h6">{{ inoperableValves.length }}</div>
                        <div class="text-caption">Inoperable</div>
                    </q-card-section>
                </q-card>
            </div>
        </div>

        <!-- Create/Edit Dialog -->
        <q-dialog v-model="editDialogOpen" persistent>
            <valve-edit-form
                v-if="!isCreating && selectedValve"
                :valve="selectedValve"
                @saved="onFormSaved"
                @deleted="onValveDeleted"
            />
            <valve-create-form
                v-if="isCreating"
                @saved="onFormSaved"
            />
        </q-dialog>
        
        <!-- Logs Dialog -->
        <q-dialog v-model="logsDialogOpen">
            <valve-logs-dialog
                v-if="selectedValve"
                :valve="selectedValve"
            />
        </q-dialog>
    </div>
</template>

<script>
import { defineComponent, ref, computed, onMounted } from 'vue';
import { useValveStore, useDauStore } from 'src/stores';
import { date } from 'quasar';
import ValveEditForm from 'src/components/ValveEditForm.vue';
import CreateValveForm from 'src/components/CreateValveForm.vue'; 
import ValveLogs from 'src/components/ValveLogs.vue';
import DataCollection from 'src/components/DataCollection.vue';

export default defineComponent({
    name: 'ValveDisplay',
    components: {
        'valve-edit-form': ValveEditForm,
        'valve-create-form': CreateValveForm,
        'valve-logs-dialog': ValveLogs,
        'data-collection': DataCollection
    },
    setup() {
        const valveStore = useValveStore();
        const dauStore = useDauStore();
        const editDialogOpen = ref(false);
        const logsDialogOpen = ref(false);
        const isCreating = ref(false);
        const selectedValve = ref(null);
    
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
        
        function getValveStatus(valve) {
            // Case: Missing one or both DAUs
            if (!valve.atv || !valve.remote) {
                return 'Inoperable';
            }
            
            // Case: One or both DAUs offline
            if (valve.atv.status === 3 || valve.remote.status === 3) {
                return 'Offline';
            }
            
            // Case: One or both DAUs need attention (maintenance or calibration)
            if (valve.atv.status === 1 || valve.atv.status === 2 || 
                valve.remote.status === 1 || valve.remote.status === 2) {
                return 'Needs Attention';
            }
            
            // Case: Both DAUs operational
            return valve.isActive ? 'Active' : 'Inactive';
        }
        
        function getValveStatusColor(valve) {
            const status = getValveStatus(valve);
            
            const statusColors = {
                'Inoperable': 'negative',
                'Offline': 'negative',
                'Needs Attention': 'warning',
                'Active': 'positive',
                'Inactive': 'grey'
            };
            
            return statusColors[status] || 'grey';
        }

        function openCreateForm() {
            isCreating.value = true;
            editDialogOpen.value = true;
        }

        function editValve(valve) {
            isCreating.value = false;
            selectedValve.value = valve;
            editDialogOpen.value = true;
        }

        function showLogs(valve) {
            selectedValve.value = valve;
            logsDialogOpen.value = true;
        }

        async function onFormSaved() {
            editDialogOpen.value = false;
            await refreshData();
        }
        
        function onValveDeleted() {
            editDialogOpen.value = false;
            refreshData();
        }

        async function refreshData() {
            await Promise.all([
                valveStore.fetchValves(),
                dauStore.fetchDaus()
            ]);
        }

        onMounted(async () => {
            try {
                await refreshData();
            } catch (error) {
                console.error('Failed to fetch data:', error);
            }
        });
        
        return {
            allValves: computed(() => valveStore.allValves),
            activeValves: computed(() => valveStore.activeValves),
            valvesNeedingAttention: computed(() => valveStore.valvesNeedingAttention),
            offlineValves: computed(() => valveStore.offlineValves),
            inoperableValves: computed(() => valveStore.inoperableValves),
            isLoading: computed(() => valveStore.isLoading),
            editDialogOpen,
            logsDialogOpen,
            isCreating,
            selectedValve,
            formatDate,
            getDauStatusColor,
            mapStatusEnum,
            getValveStatus,
            getValveStatusColor,
            openCreateForm,
            editValve,
            showLogs,
            onFormSaved,
            onValveDeleted
        };
    }
});
</script>

<style scoped>
.valve-card {
    height: 100%;
    transition: transform 0.2s, box-shadow 0.2s;
}

.valve-card:hover {
    transform: translateY(-4px);
    box-shadow: 0 8px 16px rgba(0, 0, 0, 0.1);
}
</style>
