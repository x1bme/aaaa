<template>
    <div class="valve-display q-pa-md">
        <div class="row justify-between items-center q-md-md">
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
            <p>No valves found. Create a new valve to get started.</p>
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
                                <q-badge :color="getValveStatusColor(valve)">{{ getValveStatus(valve) }}</q-badge>
                                <q-badge :color="getValveStatusColor(valve)">{{ getValveStatus(valve) }}</q-badge>
                            </div>
                            <div>
                                <q-btn flat round color="grey" icon="history" @click="showLogs(valve)"/>
                                <q-btn flat round color="primary" icon="build" @click="editValve(valve)"/>
                            </div>
                        </div>
                        <div class="text-subtitle2 q-mt-sm">
                            Location: {{ valve.location || 'Not specified' }}
                            Location: {{ valve.location || 'Not specified' }}
                        </div>
                        <div class="text=caption">
                            Installed: {{ formatDate(valve.installationDate) }}
                        </div>
                    </q-card-section>
                    <q-separator />
                    <q-card-section>
                        <div class="text-subtitle2">ATV DAU</div>
                        <q-list dense>
                            <q-item v-if="valve.atv">
                                <q-item-section>Serial Number:</q-item-section>
                                <q-item-section>{{ valve.atv?.serialNumber || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item v-if="valve.atv">
                                <q-item-section>IP Address:</q-item-section>
                                <q-item-section>{{ valve.atv?.dauIPAddress || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item v-if="valve.atv">
                                <q-item-section>Status: </q-item-section>
                                <q-item-section>
                                    <q-badge :color="getDauStatusColor(valve.atv?.status)">
                                        {{ mapStatusEnum(valve.atv?.status) }}
                                    </q-badge>
                                </q-item-section>
                            </q-item>

                            <q-item v-if="valve.atv">
                                <q-item-section>Last Heartbeat:</q-item-section>
                                <q-item-section>{{ formatDate(valve.atv?.lastHeartbeat) }}</q-item-section>
                            </q-item>
                            
                            <q-item v-if="!valve.atv" class="text-negative">
                                <q-item-section>
                                    <q-icon name="warning" color="negative" />
                                    No ATV DAU attached!
                                </q-item-section>
                                <q-item-section>{{ formatDate(valve.atv?.lastHeartbeat) }}</q-item-section>
                            </q-item>
                            
                            <q-item v-if="!valve.atv" class="text-negative">
                                <q-item-section>
                                    <q-icon name="warning" color="negative" />
                                    No ATV DAU attached!
                                </q-item-section>
                            </q-item>
                        </q-list>
                    </q-card-section>

                    <q-separator />
                    <q-card-section>
                        <div class="text-subtitle2">Remote DAU</div>
                        <q-list dense>
                            <q-item v-if="valve.remote">
                                <q-item-section>Serial Number:</q-item-section>
                                <q-item-section>{{ valve.remote?.serialNumber || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item v-if="valve.remote">
                                <q-item-section>IP Address:</q-item-section>
                                <q-item-section>{{ valve.remote?.dauIPAddress || 'N/A' }}</q-item-section>
                            </q-item>

                            <q-item v-if="valve.remote">
                                <q-item-section>Status: </q-item-section>
                                <q-item-section>
                                    <q-badge :color="getDauStatusColor(valve.remote?.status)">
                                        {{ mapStatusEnum(valve.remote?.status) }}
                                    </q-badge>
                                </q-item-section>
                            </q-item>

                            <q-item v-if="valve.remote">
                                <q-item-section>Last Heartbeat:</q-item-section>
                                <q-item-section>{{ formatDate(valve.remote?.lastHeartbeat) }}</q-item-section>
                            </q-item>
                            
                            <q-item v-if="!valve.remote" class="text-negative">
                                <q-item-section>
                                    <q-icon name="warning" color="negative" />
                                    No Remote DAU attached!
                                </q-item-section>
                                <q-item-section>{{ formatDate(valve.remote?.lastHeartbeat) }}</q-item-section>
                            </q-item>
                            
                            <q-item v-if="!valve.remote" class="text-negative">
                                <q-item-section>
                                    <q-icon name="warning" color="negative" />
                                    No Remote DAU attached!
                                </q-item-section>
                            </q-item>
                        </q-list>
                    </q-card-section>
                    <q-separator />
                    <q-card-section>
                        <data-collection />
                    </q-card-section>
                </q-card>
            </div>
        </div>
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
            'data-collection' : DataCollection
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
                    'Operational': 'positive',
                    'NeedsCalibration' : 'info',
                    'NeedsMaintenance': 'warning',
                    'Offline' : 'negative',
                };
                return dauColors[statusValue] || 'grey';
            }

            function mapStatusEnum(enumVal) {
                const statusMapping = {
                    0: 'Operational',
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
                
                // Case: One DAU offline, one active
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
            
            function getValveStatus(valve) {
                // Case: Missing one or both DAUs
                if (!valve.atv || !valve.remote) {
                    return 'Inoperable';
                }
                
                // Case: One DAU offline, one active
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

            function onFormSaved() {
                editDialogOpen.value = false;
                valveStore.fetchValves();
            }
            
            
            function onValveDeleted() {
                editDialogOpen.value = false;
                valveStore.fetchValves();
            }

            onMounted(async () => {
                try {
                    await Promise.all([
                        valveStore.fetchValves(),
                        dauStore.fetchDaus()
                    ]);
                    await Promise.all([
                        valveStore.fetchValves(),
                        dauStore.fetchDaus()
                    ]);
                } catch (error) {
                    console.error('Failed to fetch data:', error)
                    console.error('Failed to fetch data:', error)
                }
            });
            
            return {
                allValves: computed(() => valveStore.allValves),
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
    }
</style>