<template>
  <q-page :style="{ backgroundColor: bgColor }">
    <div class="q-pa-md">
      <h4 class="q-mb-md" :style="{ color: textColor }">Loom User Settings</h4>
      
      <q-form @submit="onSubmit" class="q-gutter-md">
        <!-- Personal Information -->
        <q-card :dark="isDarkMode">
          <q-card-section>
            <div class="text-h6">Personal Information</div>
          </q-card-section>
          <q-card-section>
            <div class="row q-mb-md">
              <div class="col-12 col-sm-4">
                <q-avatar size="150px">
                  <img :src="user.profilePicture">
                </q-avatar>
                <q-file v-model="newProfilePicture" label="Upload new picture" style="max-width: 150px" @update:model-value="onFileSelected" accept="image/*">
                  <template v-slot:append>
                    <q-icon name="attach_file" />
                  </template>
                </q-file>
              </div>
              <div class="col-12 col-sm-8">
                <q-input v-model="user.name" label="Full Name" :rules="[val => !!val || 'Name is required']" :dark="isDarkMode" />
                <q-input v-model="user.email" label="Email" type="email" :rules="[val => !!val || 'Email is required', val => isValidEmail(val) || 'Invalid email format']" :dark="isDarkMode" />
                <q-input v-model="user.phone" label="Phone Number" type="tel" :dark="isDarkMode" />
                <q-input v-model="user.address" label="Plant Address" type="textarea" :dark="isDarkMode" />
              </div>
            </div>
          </q-card-section>
        </q-card>

        <!-- Role Selection -->
        <q-card :dark="isDarkMode">
          <q-card-section>
            <div class="text-h6">Role Selection</div>
          </q-card-section>
          <q-card-section>
            <q-select
              v-model="user.role"
              :options="roleOptions"
              label="Select Role"
              emit-value
              map-options
              :rules="[val => !!val || 'Role selection is required']"
              :dark="isDarkMode"
            >
              <template v-slot:option="{ itemProps, itemEvents, opt }">
                <q-item v-bind="itemProps" v-on="itemEvents">
                  <q-item-section>
                    <q-item-label>{{ opt.label }}</q-item-label>
                    <q-item-label caption>{{ opt.description }}</q-item-label>
                  </q-item-section>
                </q-item>
              </template>
            </q-select>
          </q-card-section>
        </q-card>

        <!-- Application Settings -->
        <q-card :dark="isDarkMode">
          <q-card-section>
            <div class="text-h6">Application Settings</div>
          </q-card-section>
          <q-card-section>
            <q-toggle v-model="user.logActivity" label="Enable Activity Logging" :dark="isDarkMode" />
            <q-toggle v-model="user.emailAlerts" label="Enable Email Alerts" :dark="isDarkMode" />
            <q-toggle v-model="user.heartbeatAlerts" label="Enable Heartbeat Alerts for DAUs" :dark="isDarkMode" />
            <q-slider
              v-model="darknesslevel"
              :min="0"
              :max="100"
              label
              label-always
              color="primary"
              :dark="isDarkMode"
            >
              <template v-slot:thumb-label>
                Darkness: {{ darknesslevel }}%
              </template>
            </q-slider>
          </q-card-section>
        </q-card>

        <!-- Valve-Specific Preferences -->
        <q-card :dark="isDarkMode">
          <q-card-section>
            <div class="text-h6">Valve Preferences</div>
          </q-card-section>
          <q-card-section>
            <q-select v-model="user.preferredValveTypes" :options="valveTypes" label="Preferred Valve Types" multiple :dark="isDarkMode" />
            <q-input v-model="user.defaultPressureUnit" label="Most Experience With" :dark="isDarkMode" />
            <q-toggle v-model="user.receiveValveAlerts" label="Receive Valve Alerts" :dark="isDarkMode" />
          </q-card-section>
        </q-card>

        <!-- Submit Button -->
        <div>
          <q-btn label="Save Settings" type="submit" color="primary" />
        </div>
      </q-form>
    </div>
  </q-page>
</template>

<script>
import { ref, computed } from 'vue'
import { useQuasar } from 'quasar'

export default {
  setup() {
    const $quasar = useQuasar()

    const user = ref({
      name: '',
      email: '',
      phone: '',
      address: '',
      profilePicture: 'src/assets/nuclearengineer.png', // default avatar
      role: null,
      logActivity: false,
      emailAlerts: false,
      heartbeatAlerts: false,
      preferredValveTypes: [],
      defaultPressureUnit: '',
      receiveValveAlerts: true
    })

    const darknesslevel = ref(0)
    const newProfilePicture = ref(null)

    const isDarkMode = computed(() => darknesslevel.value > 50)

    const bgColor = computed(() => {
      const lightness = 100 - darknesslevel.value
      return `hsl(0, 0%, ${lightness}%)`
    })

    const textColor = computed(() => {
      return darknesslevel.value > 50 ? '#ffffff' : '#000000'
    })

    const roleOptions = [
      { label: 'Engineer', value: 'engineer', description: 'Designs and oversees valve systems' },
      { label: 'Technician', value: 'technician', description: 'Installs and maintains valves' },
      { label: 'Service Technician', value: 'serviceTechnician', description: 'Provides on-site valve servicing' },
      { label: 'Network Administrator', value: 'networkAdmin', description: 'Manages valve network infrastructure' }
    ]

    const valveTypes = ['Ball Valve', 'Gate Valve', 'Globe Valve', 'Butterfly Valve', 'Check Valve', 'Pressure Relief Valve']

    const isValidEmail = (val) => {
      const emailPattern = /^(?=[a-zA-Z0-9@._%+-]{6,254}$)[a-zA-Z0-9._%+-]{1,64}@(?:[a-zA-Z0-9-]{1,63}\.){1,8}[a-zA-Z]{2,63}$/
      return emailPattern.test(val)
    }

    const onFileSelected = (file) => {
      if (file) {
        const reader = new FileReader()
        reader.onload = (e) => {
          user.value.profilePicture = e.target.result
        }
        reader.readAsDataURL(file)
      }
    }

    const onSubmit = () => {
      // Here you would typically send the user data to your backend
      console.log('Submitting user settings:', user.value)
      $quasar.notify({
        color: 'positive',
        message: 'Settings saved successfully',
        icon: 'check'
      })
    }

    return {
      user,
      darknesslevel,
      isDarkMode,
      bgColor,
      textColor,
      newProfilePicture,
      roleOptions,
      valveTypes,
      isValidEmail,
      onFileSelected,
      onSubmit
    }
  }
}
</script>

<style lang="scss">
.q-page {
  transition: background-color 0.3s ease, color 0.3s ease;
}
</style>
