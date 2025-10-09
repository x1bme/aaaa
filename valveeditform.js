<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Edit Valve</div>
            <q-space/>
            <q-btn icon="close" flat round dense v-close-popup/>
        </q-card-section>
        <q-card-section>
            <q-form @submit="submitForm">
                <q-input v-model="form.name" label="Valve Name" :rules="[val => !!val || 'Name is required']" class="q-mb-md" />
                <q-input v-model="form.location" label="Location" class="q-mb-md" />
                
                <!-- ATV DAU Section -->
                <div class="text-subtitle2 q-mb-sm">ATV DAU</div>
                <div class="q-mb-md">
                    <q-item v-if="displayedAtvDau" bordered class="q-mb-sm">
                        <q-item-section>
                            <div class="row items-center justify-between">
                                <div>
                                    <strong>{{ displayedAtvDau.dauTag || displayedAtvDau.dauId }}</strong> 
                                    ({{ displayedAtvDau.dauIPAddress }})
                                    <q-badge :color="getDauStatusColor(displayedAtvDau.status)">
                                        {{ mapStatusEnum(displayedAtvDau.status) }}
                                    </q-badge>
                                    <q-badge v-if="displayedAtvDau.id !== valve.atv?.id" color="blue" class="q-ml-xs">
                                        Pending Change
                                    </q-badge>
                                </div>
                                <q-btn flat dense color="negative" icon="link_off" @click="detachDau('atv')" label="Detach" />
                            </div>
                        </q-item-section>
                    </q-item>
                    <div v-if="!displayedAtvDau">
                        <q-select 
                            v-model="form.atvId"
                            :options="availableDausForAtv"
                            option-label="displayName"
                            option-value="id"
                            emit-value
                            map-options
                            label="Select ATV DAU"
                            :loading="isLoading"
                        >
                            <template v-slot:no-option>
                                <q-item>
                                    <q-item-section class="text-grey">
                                        No Available DAUs
                                    </q-item-section>
                                </q-item>
                            </template>
                        </q-select>
                        <div v-if="form.atvId === 0" class="text-negative q-mt-sm">
                            <q-icon name="warning" />
                            DAU will be detached after update
                        </div>
                    </div>
                </div>

                <!-- Remote DAU Section -->
                <div class="text-subtitle2 q-mb-sm">Remote DAU</div>
                <div class="q-mb-md">
                    <q-item v-if="displayedRemoteDau" bordered class="q-mb-sm">
                        <q-item-section>
                            <div class="row items-center justify-between">
                                <div>
                                    <strong>{{ displayedRemoteDau.dauTag || displayedRemoteDau.dauId }}</strong> 
                                    ({{ displayedRemoteDau.dauIPAddress }})
                                    <q-badge :color="getDauStatusColor(displayedRemoteDau.status)">
                                        {{ mapStatusEnum(displayedRemoteDau.status) }}
                                    </q-badge>
                                    <q-badge v-if="displayedRemoteDau.id !== valve.remote?.id" color="blue" class="q-ml-xs">
                                        Pending Change
                                    </q-badge>
                                </div>
                                <q-btn flat dense color="negative" icon="link_off" @click="detachDau('remote')" label="Detach" />
                            </div>
                        </q-item-section>
                    </q-item>
                    <div v-if="!displayedRemoteDau">
                        <q-select 
                            v-model="form.remoteId"
                            :options="availableDausForRemote"
                            option-label="displayName"
                            option-value="id"
                            emit-value
                            map-options
                            label="Select Remote DAU"
                            :loading="isLoading"
                        >
                            <template v-slot:no-option>
                                <q-item>
                                    <q-item-section class="text-grey">
                                        No Available DAUs
                                    </q-item-section>
                                </q-item>
                            </template>
                        </q-select>
                        <div v-if="form.remoteId === 0" class="text-negative q-mt-sm">
                            <q-icon name="warning" />
                            DAU will be detached after update
                        </div>
                    </div>
                </div>
                
                <q-toggle v-model="form.isActive" label="Active" class="q-mb-md" />
                
                <div class="row justify-end">
                    <q-btn label="Delete Valve" color="negative" flat class="q-mr-sm" @click="showDeleteConfirm = true" />
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn label="Update" color="primary" type="submit" />
                </div>
            </q-form>
        </q-card-section>
        <valve-delete-confirm v-model="showDeleteConfirm" :valve="valve" @deleted="onValveDeleted" />
    </q-card>
</template>

<script>
import { defineComponent, ref, watch, computed, onMounted } from 'vue';
import { useValveStore, useDauStore } from 'src/stores';
import ValveDeleteConfirm from './ValveDeleteForm.vue';

export default defineComponent({
    name: 'ValveEditForm',
    components: {
        'valve-delete-confirm': ValveDeleteConfirm
    },
    props: {
        valve: {
            type: Object,
            required: true
        }
    },
    emits: ['update:modelValue', 'saved', 'deleted'],
    setup(props, { emit }) {
        const valveStore = useValveStore();
        const dauStore = useDauStore();
        const showDeleteConfirm = ref(false);
        
        const form = ref({
            id: null,
            name: '',
            location: '',
            installationDate: '',
            isActive: false,
            atvId: null,
            remoteId: null
        });
        
        // Fetch DAUs on component mount
        onMounted(async () => {
            if (dauStore.daus.length === 0) {
                await dauStore.fetchDaus();
            }
        });
        
        // Get available DAUs for ATV (not attached to any valve, OR attached to THIS valve)
        // Exclude currently selected Remote
        const availableDausForAtv = computed(() => {
            const daus = dauStore.daus
                .filter(dau => 
                    dau.registered && 
                    (
                        !dau.valveId || 
                        dau.valveId === 0 || 
                        dau.valveId === props.valve.id
                    ) &&
                    dau.id !== form.value.remoteId // Exclude currently selected Remote
                )
                .map(dau => ({
                    ...dau,
                    displayName: `${dau.dauTag || dau.dauId} (${dau.dauIPAddress})`
                }));
                
            // Add "Detach" option if currently has ATV
            if (props.valve.atv && props.valve.atv.id) {
                return [{ id: 0, displayName: 'No DAU (Detach)', dauId: 'None' }, ...daus];
            }
            return daus;
        });
        
        // Get available DAUs for Remote (not attached to any valve, OR attached to THIS valve)
        // Exclude currently selected ATV
        const availableDausForRemote = computed(() => {
            const daus = dauStore.daus
                .filter(dau => 
                    dau.registered && 
                    (
                        !dau.valveId || 
                        dau.valveId === 0 || 
                        dau.valveId === props.valve.id
                    ) &&
                    dau.id !== form.value.atvId // Exclude currently selected ATV
                )
                .map(dau => ({
                    ...dau,
                    displayName: `${dau.dauTag || dau.dauId} (${dau.dauIPAddress})`
                }));
                
            // Add "Detach" option if currently has Remote
            if (props.valve.remote && props.valve.remote.id) {
                return [{ id: 0, displayName: 'No DAU (Detach)', dauId: 'None' }, ...daus];
            }
            return daus;
        });

        // Show the currently selected or attached ATV DAU
        const displayedAtvDau = computed(() => {
            if (form.value.atvId === 0) {
                return null;
            }
            if (form.value.atvId && form.value.atvId !== props.valve.atv?.id) {
                return dauStore.daus.find(dau => dau.id === form.value.atvId) || null;
            }
            return props.valve.atv;
        });

        // Show the currently selected or attached Remote DAU
        const displayedRemoteDau = computed(() => {
            if (form.value.remoteId === 0) {
                return null;
            }
            if (form.value.remoteId && form.value.remoteId !== props.valve.remote?.id) {
                return dauStore.daus.find(dau => dau.id === form.value.remoteId) || null;
            }
            return props.valve.remote;
        });

        const isLoading = computed(() => dauStore.isLoading || valveStore.isLoading);
        
        // Watch for changes to the valve prop and update form
        watch(() => props.valve, (newValve) => {
            if (newValve) {
                form.value = {
                    id: newValve.id,
                    name: newValve.name,
                    location: newValve.location || '',
                    installationDate: newValve.installationDate,
                    isActive: newValve.isActive,
                    atvId: newValve.atv?.id || null,
                    remoteId: newValve.remote?.id || null
                };
            }
        }, { immediate: true });
        
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
        
        // Detach a DAU from the valve
        async function detachDau(dauType) {
            if (dauType === 'atv') {
                form.value.atvId = 0;
            } else {
                form.value.remoteId = 0;
            }
        }
        
        // Submit the form to update the valve
        async function submitForm() {
            try {
                // Validate that ATV and Remote are not the same
                if (form.value.atvId && form.value.remoteId && 
                    form.value.atvId === form.value.remoteId && 
                    form.value.atvId !== 0) {
                    alert('Cannot use the same DAU for both ATV and Remote positions');
                    return;
                }
                
                const updatePayload = {
                    name: form.value.name,
                    location: form.value.location,
                    installationDate: form.value.installationDate,
                    isActive: form.value.isActive,
                    atvId: form.value.atvId === 0 ? null : form.value.atvId,
                    remoteId: form.value.remoteId === 0 ? null : form.value.remoteId,
                };
                
                await valveStore.updateValve({
                    id: form.value.id,
                    valve: updatePayload
                });
                
                emit('saved');
            } catch (error) {
                console.error('Error updating valve:', error);
                alert(`Failed to update valve: ${error.message || 'Please check that the selected DAUs are available.'}`);
            }
        }

        function onValveDeleted() {
            emit('deleted');
        }
        
        return {
            form,
            isLoading,
            availableDausForAtv,
            availableDausForRemote,
            submitForm,
            detachDau,
            showDeleteConfirm,
            onValveDeleted,
            getDauStatusColor,
            displayedAtvDau,
            displayedRemoteDau,
            mapStatusEnum
        };
    }
});
</script>

<style scoped>
/* Optional: Add some spacing for better visual separation */
.q-item {
    border-radius: 4px;
}
</style>
