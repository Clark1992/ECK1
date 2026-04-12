import { useState } from 'react';
import {
  Dialog, DialogTitle, DialogContent, DialogActions,
  TextField, Button, Box, Typography, IconButton, Chip,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import DeleteIcon from '@mui/icons-material/Delete';
import type { CreateSample2Request } from '../../types/sample2';

interface Props {
  open: boolean;
  onClose: () => void;
  onSubmit: (data: CreateSample2Request) => void;
  loading?: boolean;
}

interface LineItemForm {
  sku: string;
  quantity: string;
  amount: string;
  currency: string;
}

export default function Sample2FormDialog({ open, onClose, onSubmit, loading }: Props) {
  const [email, setEmail] = useState('');
  const [segment, setSegment] = useState('');
  const [street, setStreet] = useState('');
  const [city, setCity] = useState('');
  const [country, setCountry] = useState('');
  const [lineItems, setLineItems] = useState<LineItemForm[]>([
    { sku: '', quantity: '1', amount: '0', currency: 'USD' },
  ]);
  const [tagInput, setTagInput] = useState('');
  const [tags, setTags] = useState<string[]>([]);

  const handleAddLineItem = () => {
    setLineItems([...lineItems, { sku: '', quantity: '1', amount: '0', currency: 'USD' }]);
  };

  const handleRemoveLineItem = (idx: number) => {
    setLineItems(lineItems.filter((_, i) => i !== idx));
  };

  const updateLineItem = (idx: number, field: keyof LineItemForm, value: string) => {
    const updated = [...lineItems];
    updated[idx] = { ...updated[idx]!, [field]: value };
    setLineItems(updated);
  };

  const addTag = () => {
    const t = tagInput.trim();
    if (t && !tags.includes(t)) {
      setTags([...tags, t]);
      setTagInput('');
    }
  };

  const handleSubmit = () => {
    onSubmit({
      customer: { email, segment },
      shippingAddress: { street, city, country },
      lineItems: lineItems.map((li) => ({
        sku: li.sku,
        quantity: parseInt(li.quantity, 10) || 1,
        unitPrice: { amount: parseFloat(li.amount) || 0, currency: li.currency },
      })),
      tags,
    });
  };

  const handleClose = () => {
    setEmail(''); setSegment(''); setStreet(''); setCity(''); setCountry('');
    setLineItems([{ sku: '', quantity: '1', amount: '0', currency: 'USD' }]);
    setTags([]); setTagInput('');
    onClose();
  };

  const valid = email && segment && street && city && country && lineItems.every((li) => li.sku);

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="md" fullWidth>
      <DialogTitle>Create Order</DialogTitle>
      <DialogContent>
        <Box display="flex" flexDirection="column" gap={2} mt={1}>
          <Typography variant="subtitle2">Customer</Typography>
          <Box display="flex" gap={2}>
            <TextField label="Email" value={email} onChange={(e) => setEmail(e.target.value)} required fullWidth />
            <TextField label="Segment" value={segment} onChange={(e) => setSegment(e.target.value)} required fullWidth />
          </Box>

          <Typography variant="subtitle2">Shipping Address</Typography>
          <Box display="flex" gap={2}>
            <TextField label="Street" value={street} onChange={(e) => setStreet(e.target.value)} required fullWidth />
            <TextField label="City" value={city} onChange={(e) => setCity(e.target.value)} required fullWidth />
            <TextField label="Country" value={country} onChange={(e) => setCountry(e.target.value)} required fullWidth />
          </Box>

          <Box display="flex" justifyContent="space-between" alignItems="center">
            <Typography variant="subtitle2">Line Items</Typography>
            <IconButton size="small" onClick={handleAddLineItem}><AddIcon /></IconButton>
          </Box>
          {lineItems.map((li, idx) => (
            <Box key={idx} display="flex" gap={1} alignItems="center">
              <TextField label="SKU" size="small" value={li.sku} onChange={(e) => updateLineItem(idx, 'sku', e.target.value)} required />
              <TextField label="Qty" size="small" type="number" sx={{ width: 80 }} value={li.quantity} onChange={(e) => updateLineItem(idx, 'quantity', e.target.value)} />
              <TextField label="Price" size="small" type="number" sx={{ width: 100 }} value={li.amount} onChange={(e) => updateLineItem(idx, 'amount', e.target.value)} />
              <TextField label="Currency" size="small" sx={{ width: 80 }} value={li.currency} onChange={(e) => updateLineItem(idx, 'currency', e.target.value)} />
              {lineItems.length > 1 && (
                <IconButton size="small" onClick={() => handleRemoveLineItem(idx)}><DeleteIcon /></IconButton>
              )}
            </Box>
          ))}

          <Typography variant="subtitle2">Tags</Typography>
          <Box display="flex" gap={1}>
            <TextField
              size="small" placeholder="Add tag" value={tagInput}
              onChange={(e) => setTagInput(e.target.value)}
              onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); addTag(); } }}
            />
            <Button size="small" onClick={addTag}>Add</Button>
          </Box>
          <Box display="flex" gap={0.5} flexWrap="wrap">
            {tags.map((t) => (
              <Chip key={t} label={t} size="small" onDelete={() => setTags(tags.filter((x) => x !== t))} />
            ))}
          </Box>
        </Box>
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancel</Button>
        <Button variant="contained" onClick={handleSubmit} disabled={!valid || loading}>
          Create
        </Button>
      </DialogActions>
    </Dialog>
  );
}
