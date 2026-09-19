import { useState } from 'react';
import { Outlet, useNavigate, Link } from 'react-router-dom';
import { Layout, Menu, Button, Typography } from 'antd';
import {
    HomeOutlined, TeamOutlined, TrophyOutlined, BankOutlined,
    EnvironmentOutlined, MessageOutlined, LogoutOutlined,
} from '@ant-design/icons';
import { useAuth } from '../hooks/useAuth';
import { logout } from '../api/auth';

const { Header, Sider, Content } = Layout;
const { Text } = Typography;

// Maps each backend role (matching the C# UserRole enum's exact string
// values) to the set of dashboard sections that role is allowed to see.
// This must stay in sync with the [Authorize(Roles = "...")] attributes
// on each controller — this list only controls what's SHOWN in the menu;
// the real enforcement always happens server-side regardless of what
// this object says.
const ROLE_MENU = {
    Admin: ['facilities', 'champions', 'federations', 'athletes', 'locations', 'users', 'messages'],
    Manager: ['facilities', 'champions', 'federations', 'athletes', 'locations', 'users', 'messages'],
    M88Officer: ['facilities', 'messages'],
    M5Officer: ['facilities', 'messages'],
    InsuranceOfficer: ['athletes', 'messages'],
    SportOfficer: ['champions', 'messages'],
    FederationRep: ['messages'],
};

const ALL_MENU_ITEMS = [
    { key: 'facilities', icon: <BankOutlined />, label: <Link to="/dashboard/facilities">اماکن ورزشی</Link> },
    { key: 'champions', icon: <TrophyOutlined />, label: <Link to="/dashboard/champions">قهرمانان</Link> },
    { key: 'federations', icon: <TeamOutlined />, label: <Link to="/dashboard/federations">فدراسیون‌ها</Link> },
    { key: 'athletes', icon: <HomeOutlined />, label: <Link to="/dashboard/athletes">آمار ورزشکاران</Link> },
    { key: 'locations', icon: <EnvironmentOutlined />, label: <Link to="/dashboard/locations">استان‌ها و شهرها</Link> },
    { key: 'users', icon: <TeamOutlined />, label: <Link to="/dashboard/users">کاربران</Link> },
    { key: 'messages', icon: <MessageOutlined />, label: <Link to="/dashboard/messages">پیام‌ها</Link> },
];

export default function DashboardLayout() {
    const [collapsed, setCollapsed] = useState(false);
    const { role, fullName } = useAuth();
    const navigate = useNavigate();

    const allowedKeys = ROLE_MENU[role] || [];
    const visibleItems = ALL_MENU_ITEMS.filter((item) => allowedKeys.includes(item.key));

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    return (
        <Layout style={{ minHeight: '100vh' }}>
            <Sider collapsible collapsed={collapsed} onCollapse={setCollapsed}>
                <div style={{ color: 'white', textAlign: 'center', padding: 16, fontWeight: 'bold' }}>
                    {collapsed ? 'پنل' : 'پنل مدیریت'}
                </div>
                <Menu theme="dark" mode="inline" items={visibleItems} />
            </Sider>
            <Layout>
                <Header style={{ background: '#fff', padding: '0 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Text strong>{fullName}</Text>
                    <Button icon={<LogoutOutlined />} onClick={handleLogout}>خروج</Button>
                </Header>
                <Content style={{ margin: 24 }}>
                    <Outlet />
                </Content>
            </Layout>
        </Layout>
    );
}
