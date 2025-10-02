<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Edit DAU</div>
            <q-space/>
            <q-btn icon="close" flat round dense v-close-popup/>
        </q-card-section>
        <q-card-section>
            <q-form @submit="submitForm">
                <q-input v-model="form.serialNumber" label="Serial Number" :rules="[val => !!val || 'Serial Number is required']" class="q-mb-md" />
                <q-input v-model="form.location" label="Location" class="q-mb-md" />
                <q-input v-model="form.dauIPAddress" label="IP Address" :rules="[
                    val => !!val || 'IP Address is required',
                    val => validateIpAddress(val) || 'Invalid IP address format'
                ]" class="q-mb-md" />
                <q-input v-model.number="form.tcpPort" type="number" label="TCP Port" :rules="[
                    val => val !== null || 'TCP Port is required',
                    val => (val >= 1 && val <= 65535) || 'Port must be between 1 and 65535'
                ]" class="q-mb-md" />
                <q-input v-model.number="form.udpPort" type="number" label="UDP Port" :rules="[
                    val => val !== null || 'UDP Port is required',
                    val => (val >= 1 && val <= 65535) || 'Port must be between 1 and 65535'
                ]" class="q-mb-md" />
                <q-select
                    v-model="form.status"
                    :options="statusOptions"
                    option-label="label"
                    option-value="value"
                    emit-value
                    map-options
                    label="Status"
                    class="q-mb-md"
                />
                <q-toggle v-model="form.heartBeatAlerts" label="Enable Heartbeat Alerts" class="q-mb-md" />
                
                <q-input v-model="form.lastCalibration" type="date" label="Last Calibration Date" class="q-mb-md" />
                <q-input v-model="form.nextCalibration" type="date" label="Next Calibration Date" class="q-mb-md" />
                
                <div class="row justify-end">
                    <q-btn 
                        v-if="dau.valveId" 
                        label="Detach from Valve" 
                        color="warning" 
                        flat 
                        class="q-mr-sm" 
                        @click="detachFromValve" 
                    />
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn label="Update" color="primary" type="submit" />
                </div>
            </q-form>
        </q-card-section>
    </q-card>
</template>

<script>
import { defineComponent, ref, watch } from 'vue';
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
            serialNumber: '',
            location: '',
            dauIPAddress: '',
            heartBeatAlerts: true,
            status: 0,
            tcpPort: 3000,
            udpPort: 5000,
            lastHeartbeat: '',
            lastCalibration: '',
            nextCalibration: '',
            valveId: null
        });

        const statusOptions = [
            { label: 'Operational', value: 0 },
            { label: 'Needs Calibration', value: 1 },
            { label: 'Needs Maintenance', value: 2 },
            { label: 'Offline', value: 3 }
        ];

        watch(() => props.dau, (newDau) => {
            if (newDau) {
                // Format dates for input fields
                const lastCalDate = newDau.lastCalibration ? 
                    date.formatDate(new Date(newDau.lastCalibration), 'YYYY-MM-DD') : 
                    date.formatDate(new Date(), 'YYYY-MM-DD');
                
                const nextCalDate = newDau.nextCalibration ? 
                    date.formatDate(new Date(newDau.nextCalibration), 'YYYY-MM-DD') : 
                    date.formatDate(new Date(new Date().setMonth(new Date().getMonth() + 3)), 'YYYY-MM-DD');
                
                form.value = {
                    id: newDau.id,
                    serialNumber: newDau.serialNumber || '',
                    location: newDau.location || '',
                    dauIPAddress: newDau.dauIPAddress || '',
                    heartBeatAlerts: newDau.heartBeatAlerts || false,
                    status: newDau.status || 0,
                    tcpPort: newDau.tcpPort || 3000,
                    udpPort: newDau.udpPort || 5000,
                    lastHeartbeat: newDau.lastHeartbeat || new Date().toISOString(),
                    lastCalibration: lastCalDate,
                    nextCalibration: nextCalDate,
                    valveId: newDau.valveId
                };
            }
        }, { immediate: true });

        function validateIpAddress(ip) {
            // Simple regex for IP validation
            const ipRegex = /^(\d{1,3}\.){3}\d{1,3}$/;
            if (!ipRegex.test(ip)) return false;
            
            // Check each octet is between 0-255
            const octets = ip.split('.');
            return octets.every(octet => {
                const num = parseInt(octet, 10);
                return num >= 0 && num <= 255;
            });
        }

        async function submitForm() {
            try {
                // Convert date input values to ISO strings
                const updatedDau = {
                    ...form.value,
                    lastCalibration: new Date(form.value.lastCalibration).toISOString(),
                    nextCalibration: new Date(form.value.nextCalibration).toISOString()
                };
                
                await dauStore.updateDau(form.value.id, updatedDau);
                emit('saved');
            } catch (error) {
                console.error('Error updating DAU:', error);
            }
        }

        async function detachFromValve() {
            try {
                // Create a copy of the form data with valveId set to null
                const updatedDau = {
                    ...form.value,
                    valveId: null,
                    lastCalibration: new Date(form.value.lastCalibration).toISOString(),
                    nextCalibration: new Date(form.value.nextCalibration).toISOString()
                };
                
                await dauStore.updateDau(form.value.id, updatedDau);
                emit('saved');
            } catch (error) {
                console.error('Error detaching DAU from valve:', error);
            }
        }

        return {
            form,
            statusOptions,
            validateIpAddress,
            submitForm,
            detachFromValve
        };
    }
});
</script>