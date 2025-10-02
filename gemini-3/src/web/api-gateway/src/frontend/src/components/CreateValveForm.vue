<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Create New Valve</div>
            <q-space/>
            <q-btn icon="close" flat round dense v-close-popup/>
        </q-card-section>
        <q-card-section>
            <q-form @submit="submitForm">
                <q-input v-model="form.name" label="Valve Name" :rules="[val => !!val || 'Name is required']" class="q-mb-md" />
                <q-input v-model="form.location" label="Location" class="q-mb-md" />
                
                <div class="text-subtitle2 q-mb-sm">ATV DAU</div>
                <q-select 
                    v-model="form.atvId"
                    :options="availableDaus"
                    option-label="displayName"
                    option-value="id"
                    emit-value
                    map-options
                    label="Select ATV DAU"
                    :rules="[val => !!val || 'ATV DAU is required']"
                    class="q-mb-md"
                    :loading="isLoading"
                    :disable="isLoading || !availableDaus.length"
                >
                    <template v-slot:no-option>
                        <q-item>
                            <q-item-section class="text-grey">
                                No available DAUs. Please create DAUs first.
                            </q-item-section>
                        </q-item>
                    </template>
                </q-select>
                
                <div class="text-subtitle2 q-mb-sm">Remote DAU</div>
                <q-select 
                    v-model="form.remoteId"
                    :options="availableDaus"
                    option-label="displayName"
                    option-value="id"
                    emit-value
                    map-options
                    label="Select Remote DAU"
                    :rules="[val => !!val || 'Remote DAU is required']"
                    class="q-mb-md"
                    :loading="isLoading"
                    :disable="isLoading || !availableDaus.length"
                >
                    <template v-slot:no-option>
                        <q-item>
                            <q-item-section class="text-grey">
                                No available DAUs. Please create DAUs first.
                            </q-item-section>
                        </q-item>
                    </template>
                </q-select>
                
                <q-toggle v-model="form.isActive" label="Active" class="q-mb-md" />
                
                <div v-if="availableDaus.length < 2" class="text-negative q-mb-md">
                    <q-icon name="warning" /> You need at least two available DAUs to create a valve.
                </div>
                
                <div class="row justify-end">
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn label="Create" color="primary" type="submit" :disable="availableDaus.length < 2" />
                </div>
            </q-form>
        </q-card-section>
    </q-card>
</template>

<script>
    import { defineComponent, ref, computed, onMounted } from 'vue';
    import { useValveStore, useDauStore } from 'src/stores';
    
    export default defineComponent({
        name: 'ValveCreationForm',
        emits: ['saved'],
        setup(props, { emit }) {
            const valveStore = useValveStore();
            const dauStore = useDauStore();
            
            const form = ref({
                name: '',
                location: '',
                isActive: true,
                atvId: null,
                remoteId: null,
                configurations: [],
                logs: []
            });
            
            // Fetch DAUs on component mount
            onMounted(async () => {
                if (dauStore.daus.length === 0) {
                    await dauStore.fetchDaus();
                }
            });
            
            // Computed property to get only available DAUs (not attached to any valve)
            const availableDaus = computed(() => {
                return dauStore.daus
                    .filter(dau => !dau.valveId || dau.valveId === 0 || dau.valveId === null)
                    .map(dau => ({
                        ...dau,
                        displayName: `${dau.serialNumber} (${dau.dauIPAddress})`
                    }));
            });
            
            const isLoading = computed(() => dauStore.isLoading);
            
            async function submitForm() {
                try {
                    if (form.value.atvId === form.value.remoteId) {
                        // Show error - can't use the same DAU for both positions
                        alert('Cannot use the same DAU for both ATV and Remote positions');
                        return;
                    }
                    
                    const atv = await dauStore.fetchDauById(form.value.atvId);
                    const remote = await dauStore.fetchDauById(form.value.remoteId);
                    
                    const { atvId, remoteId, ...newValve } = form.value;
                    
                    const createPayload = {
                        ...newValve,
                        atv: atv,
                        atvId: atv.id,
                        remote: remote,
                        remoteId: remote.id,
                        installationDate: new Date().toISOString()
                    }
                    
                    await valveStore.createValve(createPayload);
                    emit('saved');
                } catch (error) {
                    console.error('Error creating valve:', error);
                }
            }
            
            return {
                form,
                availableDaus,
                isLoading,
                submitForm
            };
        }
    });
</script>