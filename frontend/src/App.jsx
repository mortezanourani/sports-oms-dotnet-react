import { Routes, Route } from 'react-router-dom';
import PublicLayout from './layouts/PublicLayout';
import DashboardLayout from './layouts/DashboardLayout';
import ProtectedRoute from './components/ProtectedRoute';
import Login from './pages/auth/Login';
import AthleteStats from './pages/public/AthleteStats';
import ChampionStats from './pages/public/ChampionStats';
import FacilityStats from './pages/public/FacilityStats';
import FacilitiesList from './pages/dashboard/Facilities/FacilitiesList';
import LocationsPage from './pages/dashboard/Locations/LocationsPage';
import ChampionsList from './pages/dashboard/Champions/ChampionsList';
import FederationsList from './pages/dashboard/Federations/FederationsList';
import AthleteCountsList from './pages/dashboard/Athletes/AthleteCountsList';
import UsersList from './pages/dashboard/Users/UsersList';
import MessagesPage from './pages/dashboard/Messages/MessagesPage';
import ProfilePage from './pages/dashboard/Profile/ProfilePage';

export default function App() {
  return (
      <Routes>
        <Route element={<PublicLayout />}>
          <Route path="/" element={<AthleteStats />} />
          <Route path="/athletes" element={<AthleteStats />} />
          <Route path="/champions" element={<ChampionStats />} />
          <Route path="/facilities" element={<FacilityStats />} />
          <Route path="/login" element={<Login />} />
        </Route>

        <Route element={<ProtectedRoute />}>
          <Route element={<DashboardLayout />}>
            <Route element={<ProtectedRoute allowedRoles={['Admin', 'Manager', 'M5Manager', 'M5Officer']} />}>
              <Route path="/dashboard/private-facilities" element={<FacilitiesList facilityTypes={['Private']} title="اماکن خصوصی" />} />
            </Route>
            <Route element={<ProtectedRoute allowedRoles={['Admin', 'Manager', 'M88Manager', 'M88Officer']} />}>
              <Route path="/dashboard/msy-gov-facilities" element={<FacilitiesList facilityTypes={['Msy', 'Gov']} title="اماکن MSY و دولتی" />} />
            </Route>
            <Route path="/dashboard/champions" element={<ChampionsList />} />
            <Route path="/dashboard/federations" element={<FederationsList />} />
            <Route path="/dashboard/athletes" element={<AthleteCountsList />} />
            <Route element={<ProtectedRoute allowedRoles={['Admin', 'Manager', 'HumanResourceManager', 'HumanResourceOfficer']} />}>
              <Route path="/dashboard/users" element={<UsersList />} />
            </Route>
              <Route path="/dashboard/locations" element={<LocationsPage />} />
              <Route path="/dashboard/messages" element={<MessagesPage />} />
              <Route path="/dashboard/profile" element={<ProfilePage />} />
          </Route>
        </Route>
      </Routes>
  );
}
