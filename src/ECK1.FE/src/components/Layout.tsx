import { useState, type ReactNode } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from 'react-oidc-context';
import {
  AppBar,
  Box,
  Drawer,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Toolbar,
  Typography,
  Button,
  Chip,
  Divider,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import HomeIcon from '@mui/icons-material/Home';
import ScienceIcon from '@mui/icons-material/Science';
import InventoryIcon from '@mui/icons-material/Inventory';
import { usePermissions } from '../hooks/usePermissions';

const DRAWER_WIDTH = 240;

const navItems = [
  { label: 'Home', path: '/', icon: <HomeIcon /> },
  { label: 'Samples', path: '/samples', icon: <ScienceIcon /> },
  { label: 'Orders (Sample2s)', path: '/sample2s', icon: <InventoryIcon /> },
];

export default function Layout({ children }: { children: ReactNode }) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const navigate = useNavigate();
  const location = useLocation();
  const auth = useAuth();
  const { roles } = usePermissions();

  const userName = auth.user?.profile?.preferred_username
    ?? auth.user?.profile?.name
    ?? auth.user?.profile?.email
    ?? '';

  const drawer = (
    <Box sx={{ width: DRAWER_WIDTH }} role="presentation" onClick={() => setDrawerOpen(false)}>
      <Toolbar>
        <Typography variant="h6" noWrap>
          ECK1
        </Typography>
      </Toolbar>
      <Divider />
      <List>
        {navItems.map((item) => (
          <ListItemButton
            key={item.path}
            selected={location.pathname === item.path || (item.path !== '/' && location.pathname.startsWith(item.path))}
            onClick={() => navigate(item.path)}
          >
            <ListItemIcon>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar position="fixed" sx={{ zIndex: (t) => t.zIndex.drawer + 1 }}>
        <Toolbar>
          <IconButton color="inherit" edge="start" onClick={() => setDrawerOpen(!drawerOpen)} sx={{ mr: 2 }}>
            <MenuIcon />
          </IconButton>
          <Typography variant="h6" noWrap sx={{ flexGrow: 1 }}>
            ECK Platform
          </Typography>
          {auth.isAuthenticated ? (
            <Box display="flex" alignItems="center" gap={1}>
              {[...roles].map((r) => (
                <Chip key={r} label={r} size="small" color="secondary" variant="outlined" sx={{ color: 'white', borderColor: 'rgba(255,255,255,0.5)' }} />
              ))}
              <Typography variant="body2" sx={{ mr: 1 }}>
                {userName}
              </Typography>
              <Button color="inherit" variant="outlined" size="small" onClick={() => auth.signoutRedirect()}>
                Sign out
              </Button>
            </Box>
          ) : (
            <Button
              color="inherit"
              variant="outlined"
              onClick={() => {
                sessionStorage.setItem('returnUrl', location.pathname);
                auth.signinRedirect();
              }}
            >
              Sign in
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <Drawer anchor="left" open={drawerOpen} onClose={() => setDrawerOpen(false)}>
        {drawer}
      </Drawer>
      <Box component="main" sx={{ flexGrow: 1, p: 3, mt: 8 }}>
        {children}
      </Box>
    </Box>
  );
}
