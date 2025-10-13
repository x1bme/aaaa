<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Edit DAU</div>
            <q-space/>
            <q-btn icon="close" flat round dense v-close-popup/>
        </q-card-section>
        <q-card-section>
            <q-form @submit="submitForm">
                <!-- Read-only DAU ID -->
                <q-input 
                    v-model="form.dauId" 
                    label="DAU ID" 
                    readonly
                    class="q-mb-md"
                    hint="DAU ID cannot be changed"
                >
                    <template v-slot:prepend>
                        <q-icon name="fingerprint" />
                    </template>
                </q-input>
                
                <!-- Editable DAU Tag -->
                <q-input 
                    v-model="form.dauTag" 
                    label="DAU Tag (Custom Name)" 
                    :rules="[val => !!val || 'DAU Tag is required']" 
                    class="q-mb-md"
                    hint="Custom name to identify this DAU"
                >
                    <template v-slot:prepend>
                        <q-icon name="label" />
                    </template>
                </q-input>
                
                <!-- Location -->
                <q-input 
                    v-model="form.location" 
                    label="Location" 
                    class="q-mb-md"
                >
                    <template v-slot:prepend>
                        <q-icon name="place" />
                    </template>
                </q-input>
                
                <!-- IP Address -->
                <q-input 
                    v-model="form.dauIPAddress" 
                    label="IP Address" 
                    :rules="[
                        val => !!val || 'IP Address is required',
                        val => validateIpAddress(val) || 'Invalid IP address format'
                    ]" 
                    class="q-mb-md"
                >
                    <template v-slot:prepend>
                        <q-icon name="computer" />
                    </template>
                </q-input>
                
                <q-separator class="q-my-md" />
                
                <!-- Read-only server data section -->
                <div class="text-subtitle2 q-mb-sm text-grey-7">
                    <q-icon name="cloud" size="sm" class="q-mr-xs" />
                    Live Data from DAU Server
                </div>
                
                <!-- Status Badge -->
                <q-item class="q-mb-md">
                    <q-item-section>
                        <q-item-label caption>Status</q-item-label>
                        <q-item-label>
                            <q-badge :color="getDauStatusColor(form.status)">
                                {{ mapStatusEnum(form.status) }}
                            </q-badge>
                        </q-item-label>
                    </q-item-section>
                </q-item>
                
                <!-- Last Heartbeat -->
                <q-input 
                    v-model="formattedLastHeartbeat" 
                    label="Last Heartbeat" 
                    readonly 
                    class="q-mb-md"
                >
                    <template v-slot:prepend>
                        <q-icon name="favorite" />
                    </template>
                </q-input>
                
                <!-- Last Calibration -->
                <q-input 
                    v-model="formattedLastCalibration" 
                    label="Last Calibration" 
                    readonly 
                    class="q-mb-md"
                >
                    <template v-slot:prepend>
                        <q-icon name="tune" />
                    </template>
                </q-input>
                
                <!-- Next Calibration -->
                <q-input 
                    v-model="formattedNextCalibration" 
                    label="Next Calibration" 
                    readonly 
                    class="q-mb-md"
                >
                    <template v-slot:prepend>
                        <q-icon name="schedule" />
                    </template>
                </q-input>
                
                <!-- Valve attachment info -->
                <div v-if="form.valveId" class="q-mb-md">
                    <q-banner class="bg-blue-1">
                        <template v-slot:avatar>
                            <q-icon name="link" color="blue" />
                        </template>
                        This DAU is currently attached to Valve ID: <strong>{{ form.valveId }}</strong>
                        <template v-slot:action>
                            <q-btn 
                                flat 
                                color="negative" 
                                label="Detach" 
                                @click="detachFromValve"
                                size="sm"
                            />
                        </template>
                    </q-banner>
                </div>
                
                <div class="row justify-end">
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn label="Update" color="primary" type="submit" />
                </div>
            </q-form>
        </q-card-section>
    </q-card>
</template>

<script>
import { defineComponent, ref, watch, computed } from 'vue';
import { useDauStore } from 'src/stores/dauStore';
import { date } from 'quasar';

export default defineComponent({
    name: 'DauEditForm',
    props: {
        dau: {
            type: Object,
            required: true
        }
    },
    emits: ['saved', 'deleted'],
    setup(props, { emit }) {
        const dauStore = useDauStore();
        const form = ref({
            id: null,
            dauId: '',
            dauTag: '',
            location: '',
            dauIPAddress: '',
            registered: true,
            status: 0,
            lastHeartbeat: null,
            lastCalibration: null,
            nextCalibration: null,
            valveId: null
        });

        // Computed formatted dates for display
        const formattedLastHeartbeat = computed(() => {
            return formatDate(form.value.lastHeartbeat);
        });

        const formattedLastCalibration = computed(() => {
            return formatDate(form.value.lastCalibration);
        });

        const formattedNextCalibration = computed(() => {
            return formatDate(form.value.nextCalibration);
        });

        watch(() => props.dau, (newDau) => {
            if (newDau) {
                form.value = {
                    id: newDau.id,
                    dauId: newDau.dauId || '',
                    dauTag: newDau.dauTag || '',
                    location: newDau.location || '',
                    dauIPAddress: newDau.dauIPAddress || '',
                    registered: newDau.registered || false,
                    status: newDau.status || 0,
                    lastHeartbeat: newDau.lastHeartbeat || null,
                    lastCalibration: newDau.lastCalibration || null,
                    nextCalibration: newDau.nextCalibration || null,
                    valveId: newDau.valveId || null
                };
            }
        }, { immediate: true });

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

        function validateIpAddress(ip) {
            const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
            if (!ipRegex.test(ip)) return false;
            
            const octets = ip.split('.');
            return octets.every(octet => {
                const num = parseInt(octet, 10);
                return num >= 0 && num <= 255;
            });
        }

        async function submitForm() {
            try {
                // Only send user-editable fields
                // Backend will fetch heartbeat/calibration/status from DAU Server on GET
                const updatedDau = {
                    id: form.value.id,
                    dauId: form.value.dauId,
                    dauTag: form.value.dauTag,
                    location: form.value.location,
                    dauIPAddress: form.value.dauIPAddress,
                    registered: form.value.registered,
                    valveId: form.value.valveId
                };
                
                await dauStore.updateDau(form.value.id, updatedDau);
                emit('saved');
            } catch (error) {
                console.error('Error updating DAU:', error);
            }
        }

        async function detachFromValve() {
            try {
                const updatedDau = {
                    id: form.value.id,
                    dauId: form.value.dauId,
                    dauTag: form.value.dauTag,
                    location: form.value.location,
                    dauIPAddress: form.value.dauIPAddress,
                    registered: form.value.registered,
                    valveId: null
                };
                
                await dauStore.updateDau(form.value.id, updatedDau);
                emit('saved');
            } catch (error) {
                console.error('Error detaching DAU from valve:', error);
            }
        }

        return {
            form,
            formattedLastHeartbeat,
            formattedLastCalibration,
            formattedNextCalibration,
            formatDate,
            getDauStatusColor,
            mapStatusEnum,
            validateIpAddress,
            submitForm,
            detachFromValve
        };
    }
});
</script>

<style scoped>
.q-banner {
    border-radius: 4px;
}
</style>
