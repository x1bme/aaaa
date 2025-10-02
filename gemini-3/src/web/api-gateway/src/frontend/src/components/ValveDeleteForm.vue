<template>
    <q-dialog v-model="confirmDialog" persistent>
        <q-card>
            <q-card-section class="row items-center">
                <q-avatar icon="warning" color="negative" text-color="white" />
                <span class="q-ml-sm text-h6">Delete Valve</span>
            </q-card-section>
            <q-card-section>
                Are you sure you want to delete valve "{{ valve.name }}"?
                <p class="text-caption q-mt-sm text-negative">
                    This action cannot be undone. All associated valve data including configurations and logs will be permanently deleted.
                </p>
            </q-card-section>
            <q-card-actions align="right">
                <q-btn flat label="Cancel" color="primary" v-close-popup />
                <q-btn flat label="Delete" color="negative" :loading="loading" @click="confirmDelete" />
            </q-card-actions>
        </q-card>
    </q-dialog>
</template>

<script>
    import { computed, defineComponent, ref } from 'vue';
    import { useValveStore } from 'src/stores';
    import { useQuasar } from 'quasar';

    export default defineComponent({
        name: 'ValveDeleteConfirm',
        props: {
            valve: {
                type: Object,
                required: true
            },
            modelValue: {
                type: Boolean,
                default: false
            }
        },
        emits: ['update:modelValue', 'deleted'],
        setup(props, {emit}) {
            const valveStore = useValveStore();
            const q = useQuasar();
            const loading = ref(false);

            const confirmDialog = computed({
                get: () => props.modelValue,
                set: (value) => emit('update:modelValue', value)
            });
            async function confirmDelete() {
                try {
                loading.value = true;
                await valveStore.deleteValve(props.valve.id);
                confirmDialog.value = false;
                emit('deleted');
                } catch (error) {
                    console.error('Error deleting valve:', error);
                } finally {
                    loading.value = false;
                }
            }
            return {
                confirmDialog,
                loading,
                confirmDelete
            };
        }
    });
</script>