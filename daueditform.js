async function submitForm() {
    try {
        // Only send user-editable fields
        const updatedDau = {
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
        alert(`Failed to update DAU: ${error.message}`);
    }
}

async function detachFromValve() {
    try {
        // Create update with valveId set to null
        const updatedDau = {
            dauId: form.value.dauId,
            dauTag: form.value.dauTag,
            location: form.value.location,
            dauIPAddress: form.value.dauIPAddress,
            registered: form.value.registered,
            valveId: null // Detach by setting to null
        };
        
        await dauStore.updateDau(form.value.id, updatedDau);
        
        // Update local form state
        form.value.valveId = null;
        
        emit('saved');
    } catch (error) {
        console.error('Error detaching DAU from valve:', error);
        alert(`Failed to detach DAU: ${error.message}`);
    }
}
