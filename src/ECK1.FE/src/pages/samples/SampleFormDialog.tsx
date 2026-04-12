import { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, Box,
} from '@mui/material';
import type { CreateSampleRequest } from '../../types/sample';

interface Props {
  open: boolean;
  onClose: () => void;
  onSubmit: (data: CreateSampleRequest) => void;
  loading?: boolean;
}

export default function SampleFormDialog({ open, onClose, onSubmit, loading }: Props) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [street, setStreet] = useState('');
  const [city, setCity] = useState('');
  const [country, setCountry] = useState('');

  const handleSubmit = () => {
    const hasAddress = street || city || country;
    onSubmit({
      name,
      description,
      address: hasAddress ? { street, city, country } : null,
    });
  };

  const handleClose = () => {
    setName('');
    setDescription('');
    setStreet('');
    setCity('');
    setCountry('');
    onClose();
  };

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Create Sample</DialogTitle>
      <DialogContent>
        <Box display="flex" flexDirection="column" gap={2} mt={1}>
          <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} required fullWidth />
          <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} required fullWidth multiline rows={2} />
          <TextField label="Street" value={street} onChange={(e) => setStreet(e.target.value)} fullWidth />
          <TextField label="City" value={city} onChange={(e) => setCity(e.target.value)} fullWidth />
          <TextField label="Country" value={country} onChange={(e) => setCountry(e.target.value)} fullWidth />
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={!name || !description || loading}>
          Create
        </Button>
      </DialogActions>
    </Dialog>
  );
}
