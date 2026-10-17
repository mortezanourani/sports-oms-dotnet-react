import { Outlet, useNavigate, Link } from 'react-router-dom';
import { Layout, Menu, Button, Typography, Badge, Space, ConfigProvider } from 'antd';
import {
    MessageOutlined, LogoutOutlined,
} from '@ant-design/icons';
import { useAuth } from '../hooks/useAuth';
import { logout } from '../api/auth';
import { useUnreadCount} from '../hooks/useMessages';

const { Header, Content } = Layout;
const { Text } = Typography;

// Maps each backend role (matching the C# UserRole enum's exact string
// values) to the set of dashboard sections that role is allowed to see.
// This must stay in sync with the [Authorize(Roles = "...")] attributes
// on each controller — this list only controls what's SHOWN in the menu;
// the real enforcement always happens server-side regardless of what
// this object says.
const ROLE_MENU = {
    Admin: ['privateFacilities', 'msyGovFacilities', 'champions', 'federations', 'athletes', 'locations', 'users', 'messages'],
    Manager: ['privateFacilities', 'msyGovFacilities', 'champions', 'federations', 'athletes', 'users', 'messages'],
    HumanResourceManager: ['users', 'messages'],
    HumanResourceOfficer: ['users', 'messages'],
    M88Manager: ['msyGovFacilities', 'messages'],
    M88Officer: ['msyGovFacilities', 'messages'],
    M5Manager: ['privateFacilities', 'messages'],
    M5Officer: ['privateFacilities', 'messages'],
    InsuranceOfficer: ['athletes', 'messages'],
    SportsManager: ['champions', 'federations', 'messages'],
    SportsOfficer: ['champions', 'federations', 'messages'],
    FederationRep: ['messages'],
};

const SCOPE_RESTRICTION = {
    Mastery: [],
    Province: ['locations'],
    City: ['locations'],
}

export default function DashboardLayout() {
    const { role, scopeLevel, fullName } = useAuth();
    const navigate = useNavigate();

    const { data: unreadData } = useUnreadCount();
    const ALL_MENU_ITEMS = [
        { key: 'msyGovFacilities', label: <Link to="/dashboard/msy-gov-facilities">اماکن ورزشی دولتی</Link> },
        { key: 'privateFacilities', label: <Link to="/dashboard/private-facilities">باشگاه‌های خصوصی</Link> },
        { key: 'champions', label: <Link to="/dashboard/champions">مدال‌آوران</Link> },
        { key: 'federations', label: <Link to="/dashboard/federations">فدراسیون‌ها/هیات‌ها</Link> },
        { key: 'athletes', label: <Link to="/dashboard/athletes">ورزشکاران سازمان‌یافته</Link> },
        { key: 'locations', label: <Link to="/dashboard/locations">استان‌ها و شهرها</Link> },
        { key: 'users', label: <Link to="/dashboard/users">امور اداری</Link> },
    ];

    const allowedKeys = ROLE_MENU[role] || [];
    const notAllowedKeys = SCOPE_RESTRICTION[scopeLevel] || [];
    const roleItems = ALL_MENU_ITEMS
        .filter((item) =>
            allowedKeys.includes(item.key)
        );
    const visibleItems = roleItems
        .filter((item) =>
            !notAllowedKeys.includes(item.key)
        );

    const handleLogout = () => {
        logout();
        navigate('/login');
    };

    return (
        <ConfigProvider
            theme={{
                components: {
                    Layout: {
                        headerBg: '#00498d',
                        headerHeight: 48,
                    },
                    Menu: {
                        darkItemBg: 'transparent',
                        darkItemColor: '#b0d8ff',
                        darkItemSelectedBg: 'transparent',
                        darkItemSelectedColor: '#fff',
                    },
                },
            }}
        >
            <Layout style={{ minHeight: '100vh' }}>
                <Header style={{ padding: '0 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <Space>
                        <Text strong style={{ color: 'white' }}>{fullName}</Text>
                        <Menu theme="dark" mode="horizontal" items={visibleItems} style={{ justifyContent: 'start'}} />
                    </Space>
                    <Space>
                        <Link to="/dashboard/messages">
                            <Badge count={unreadData?.unreadCount} size="small" offset={[10, 0]}>
                                <Button style={{ background: 'transparent', color: 'white' }} icon={<MessageOutlined />}>پیام‌ها</Button>
                            </Badge>
                        </Link>
                        <Button style={{ background: 'transparent' }} icon={<LogoutOutlined />} onClick={handleLogout} danger>خروج</Button>
                    </Space>
                </Header>
                <Content style={{ margin: 24 }}>
                    <Outlet />
                </Content>
            </Layout>
        </ConfigProvider>
    );
}
