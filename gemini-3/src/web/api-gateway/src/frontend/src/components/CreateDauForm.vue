<template>
    <q-card style="min-width: 500px;">
        <q-card-section class="row items-center">
            <div class="text-h6">Create New DAU</div>
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
                <div class="row justify-end">
                    <q-btn label="Cancel" color="negative" flat class="q-mr-sm" v-close-popup />
                    <q-btn label="Create" color="primary" type="submit" />
                </div>
            </q-form>
        </q-card-section>
    </q-card>
</template>

<script>
import { defineComponent, ref } from 'vue';
import { useDauStore } from 'src/stores/dauStore';

export default defineComponent({
    name: 'CreateDauForm',
    emits: ['saved'],
    setup(props, { emit }) {
        const dauStore = useDauStore();
        const form = ref({
            serialNumber: '',
            location: '',
            dauIPAddress: '',
            heartBeatAlerts: true,
            status: 0, // Operational by default
            tcpPort: 3000,
            udpPort: 5000,
            lastHeartbeat: new Date().toISOString(),
            lastCalibration: new Date().toISOString(),
            nextCalibration: new Date(new Date().setMonth(new Date().getMonth() + 3)).toISOString(),
        });

        const statusOptions = [
            { label: 'Operational', value: 0 },
            { label: 'Needs Calibration', value: 1 },
            { label: 'Needs Maintenance', value: 2 },
            { label: 'Offline', value: 3 }
        ];

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
                await dauStore.createDau(form.value);
                emit('saved');
            } catch (error) {
                console.error('Error creating DAU:', error);
            }
        }

        return {
            form,
            statusOptions,
            validateIpAddress,
            submitForm
        };
    }
});
</script>