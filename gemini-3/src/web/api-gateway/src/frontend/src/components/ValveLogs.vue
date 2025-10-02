<template>
    <q-card style="width: 700px; max-width: 90vw;">
        <q-card-section class="row items-center">
            <div class="text-h6">Logs for {{ valve?.name }}</div>
            <q-space />
            <q-btn icon="close" flat round dense v-close-popup />
        </q-card-section>
        <q-card-section class="q-pa-none">
            <q-table
            :rows="logs"
            :columns="logColumns"
            row-key="id"
            dense
            :pagination="{rowsPerPage: 0}"
            >
                <template v-slot:body-cell-logType="props">
                    <q-td :props="props">
                        <q-badge :color="getLogTypeColor(props.row.logType)">
                            {{  mapLogTypeEnum(props.row.logType)  }}
                        </q-badge>
                    </q-td>
                </template>
            </q-table>
        </q-card-section>
    </q-card>
</template>

<script>
    import { defineComponent, computed } from 'vue';
    import { date } from 'quasar';
    export default defineComponent({
        name: 'ValveLogs',
        props: {
            valve: {
                type: Object,
                required: true
            }
        },
        setup(props) {
            const logColumns = [
                {
                    name: 'timeStamp',
                    align: 'left',
                    label: 'Timestamp',
                    field: 'timeStamp',
                    sortable: true,
                    format: val => formatDate(val)
                },
                {
                    name: 'logType',
                    align: 'left',
                    label: 'Type',
                    field: 'logType',
                    sortable: true,
                    format: val => mapLogTypeEnum(val)
                },
                {
                    name: 'message',
                    align: 'left',
                    label: 'Message',
                    field: 'message'
                }
            ];
            function formatDate(dateStr) {
                if (!dateStr) return 'N/A';
                try {
                    return date.formatDate(dateStr, 'MM-DD-YYYY HH:mm');
                } catch (error) {
                    console.error('Date formatting error: ', error, dateStr);
                    return 'Invalid Date';
                }
            }
            function getLogTypeColor(logType) {
                const logTypeValue = typeof logType === 'number' ? mapLogTypeEnum(logType) : logType;
                const logColors = {
                    'Info': 'grey',
                    'Warning' : 'warning',
                    'Error' : 'negative',
                    'Maintenance': 'warning'
                };
                return logColors[logTypeValue] || 'grey';
            }
            function mapLogTypeEnum(enumVal) {
                const logTypeMapping = {
                    0: 'Info',
                    1: 'Warning',
                    2: 'Error',
                    3: 'Maintenance'
                };
                return logTypeMapping[enumVal] || 'Unknown';
            }

            const logs = computed(() => {
                if (!props.valve) return [];
                return props.valve.logs || [];
            });

            return {
                logs,
                logColumns,
                formatDate,
                getLogTypeColor,
                mapLogTypeEnum
            };
        }
    });
</script>