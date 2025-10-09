<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Create New Valve</div>
            <q-space/>
            <q-btn icon="close" flat round dense v-close-popup/>
        </q-card-section>
        <q-card-section>
            <q-form @submit="submitForm">
                <q-input 
                    v-model="form.name" 
                    label="Valve Name" 
                    :rules="[val => !!val || 'Name is required']" 
                    class="q-mb-md" 
                />
                <q-input v-model="form.location" label="Location" class="q-mb-md" />
                
                <!-- Step 1: Select ATV DAU -->
                <div class="text-subtitle2 q-mb-sm">Step 1: Select ATV DAU</div>
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
                                No available DAUs. Please register DAUs first.
                            </q-item-section>
                        </q-item>
                    </template>
                    <template v-slot:selected-item="scope">
                        <q-chip
                            removable
                            @remove="form.atvId = null"
                            color="primary"
                            text-color="white"
                        >
                            {{ scope.opt.displayName }}
                        </q-chip>
                    </template>
                </q-select>
                
                <!-- Step 2: Select Remote DAU (only shown after ATV is selected) -->
                <div v-if="form.atvId" class="text-subtitle2 q-mb-sm">Step 2: Select Remote DAU</div>
                <q-select 
                    v-if="form.atvId"
                    v-model="form.remoteId"
                    :options="availableDausForRemote"
                    option-label="displayName"
                    option-value="id"
                    emit-value
                    map-options
                    label="Select Remote DAU"
                    :rules="[val => !!val || 'Remote DAU is required']"
                    class="q-mb-md"
                    :loading="isLoading"
                    :disable="isLoading || !availableDausForRemote.length"
                >
                    <template v-slot:no-option>
                        <q-item>
                            <q-item-section class="text-grey">
                                No other available DAUs.
                            </q-item-section>
                        </q-item>
                    </template>
                    <template v-slot:selected-item="scope">
                        <q-chip
                            removable
                            @remove="form.remoteId = null"
                            color="secondary"
                            text-color="white"
                        >
                            {{ scope.opt.displayName }}
                        </q-chip>
                    </template>
                </q-select>
                
                <q-toggle v-model="form.isActive" label="Active" class="q-mb-md" />
                
                <div v-if="availableDaus.length < 2" class="text-negative q-mb-md">
                    <q-icon name="warning" /> You need at least two available DAUs to create a valve.
                </div>
                
                <div class="row justify-end">
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn 
                        label="Create" 
                        color="primary" 
                        type="submit" 
                        :disable="availableDaus.length < 2 || !form.atvId || !form.remoteId" 
                    />
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
            remoteId: null
        });
        
        // Fetch DAUs on component mount
        onMounted(async () => {
            if (dauStore.daus.length === 0) {
                await dauStore.fetchDaus();
            }
        });
        
        // Get only registered DAUs that are NOT attached to any valve
        const availableDaus = computed(() => {
            return dauStore.daus
                .filter(dau => dau.registered && (!dau.valveId || dau.valveId === 0))
                .map(dau => ({
                    ...dau,
                    displayName: `${dau.dauTag || dau.dauId} (${dau.dauIPAddress})`
                }));
        });
        
        // For Remote selection, exclude the selected ATV DAU
        const availableDausForRemote = computed(() => {
            return availableDaus.value.filter(dau => dau.id !== form.value.atvId);
        });
        
        const isLoading = computed(() => dauStore.isLoading);
        
        async function submitForm() {
            try {
                if (form.value.atvId === form.value.remoteId) {
                    alert('Cannot use the same DAU for both ATV and Remote positions');
                    return;
                }
                
                const createPayload = {
                    name: form.value.name,
                    location: form.value.location,
                    isActive: form.value.isActive,
                    atvId: form.value.atvId,
                    remoteId: form.value.remoteId,
                    installationDate: new Date().toISOString()
                };
                
                await valveStore.createValve(createPayload);
                emit('saved');
            } catch (error) {
                console.error('Error creating valve:', error);
                alert('Failed to create valve. Please check that both DAUs are available.');
            }
        }
        
        return {
            form,
            availableDaus,
            availableDausForRemote,
            isLoading,
            submitForm
        };
    }
});
</script>
