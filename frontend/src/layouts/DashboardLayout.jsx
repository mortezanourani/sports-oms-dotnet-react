import { Outlet, useLocation, useNavigate, Link } from 'react-router-dom';
import { useEffect, useState } from 'react';
import { Layout, Menu, Button, Typography, Badge, Space, ConfigProvider } from 'antd';
import {
    MessageOutlined, LogoutOutlined, UserOutlined,
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
    Admin: ['privateFacilities', 'msyGovFacilities', 'champions', 'federations', 'athletes', 'locations', 'jobPosts', 'users', 'messages'],
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

const PLACEHOLDER_ITEM = (key, label) => ({
    key,
    label,
    disabled: true,
    allowedRoles: ['Admin', 'Manager'],
});

export default function DashboardLayout() {
    const { role, scopeLevel, fullName } = useAuth();
    const navigate = useNavigate();
    const { pathname } = useLocation();
    const [displayName, setDisplayName] = useState(fullName);
    const [selectedSection, setSelectedSection] = useState(null);

    useEffect(() => {
        const handleProfileUpdate = (event) => setDisplayName(event.detail.fullName);
        window.addEventListener('profile-updated', handleProfileUpdate);
        return () => window.removeEventListener('profile-updated', handleProfileUpdate);
    }, []);

    const { data: unreadData } = useUnreadCount();
    const ALL_MENU_SECTIONS = [
        {
            key: 'management',
            label: 'حوزه مدیریت',
            children: [
                PLACEHOLDER_ITEM('technicalOffice', 'دفتر فنی'),
                PLACEHOLDER_ITEM('legal', 'حقوقی'),
                PLACEHOLDER_ITEM('publicRelations', 'روابط عمومی'),
                PLACEHOLDER_ITEM('culturalAffairs', 'امور فرهنگی'),
                PLACEHOLDER_ITEM('security', 'حراست'),
            ],
        },
        {
            key: 'resources',
            label: 'حوزه معاونت توسعه منابع',
            children: [
                { key: 'users', label: 'امور اداری', path: '/dashboard/users' },
                PLACEHOLDER_ITEM('finance', 'مالی'),
                PLACEHOLDER_ITEM('inspection', 'بازرسی'),
                { key: 'msyGovFacilities', label: 'ماده 88', path: '/dashboard/msy-gov-facilities' },
                PLACEHOLDER_ITEM('assets', 'اموال'),
                PLACEHOLDER_ITEM('informationTechnology', 'فناوری اطلاعات'),
            ],
        },
        {
            key: 'sports',
            label: 'حوزه معاونت ورزشی',
            children: [
                { key: 'privateFacilities', label: 'امور باشگاه‌ها', path: '/dashboard/private-facilities' },
                { key: 'federations', label: 'فدراسیون‌ها/هیات‌ها', path: '/dashboard/federations' },
                { key: 'champions', label: 'قهرمانی', path: '/dashboard/champions' },
                { key: 'athletes', label: 'ورزشکاران سازمان‌یافته', path: '/dashboard/athletes' },
            ],
        },
        { key: 'youth', label: 'حوزه معاونت جوانان' },
        {
            key: 'systemAdministration',
            label: 'پنل مدیریت سامانه',
            children: [
                { key: 'locations', label: 'استان‌ها و شهرها', path: '/dashboard/locations' },
                { key: 'jobPosts', label: 'پست‌های سازمانی', path: '/dashboard/job-posts' },
            ],
        },
    ];

    const allowedKeys = ROLE_MENU[role] || [];
    const notAllowedKeys = SCOPE_RESTRICTION[scopeLevel] || [];
    const visibleItems = ALL_MENU_SECTIONS.map((section) => {
        const children = section.children?.filter((item) =>
            item.disabled
                ? item.allowedRoles.includes(role)
                : allowedKeys.includes(item.key) && !notAllowedKeys.includes(item.key)
        );
        return { ...section, children: children?.length ? children : undefined };
    }).filter((section) => section.children?.length);
    const routeSection = visibleItems.find((section) =>
        section.children.some((item) => item.path === pathname)
    );
    const manuallySelectedSectionKey = selectedSection?.pathname === pathname
        && visibleItems.some((section) => section.key === selectedSection.key)
        ? selectedSection.key
        : undefined;
    const activeSectionKey = manuallySelectedSectionKey ?? routeSection?.key ?? visibleItems[0]?.key;
    const activeSection = visibleItems.find((section) => section.key === activeSectionKey);
    const activeChildKey = activeSection?.children.find((item) => item.path === pathname)?.key;

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
                        itemColor: '#333333',
                        itemSelectedColor: '#fff',
                    },
                },
            }}
        >
            <Layout style={{ minHeight: '100vh' }}>
                <Header style={{ height: 'auto', lineHeight: 'normal', padding: 0, display: 'flex', flexDirection: 'column', position: 'sticky', top: '-48px', zIndex: '999' }}>
                    <div style={{ minHeight: 48, padding: '0 24px', display: 'flex', alignItems: 'center', gap: 16 }}>
                        <Text strong style={{ color: 'white', whiteSpace: 'nowrap' }}>{displayName}</Text>
                        <div style={{ flex: 1, minWidth: 0, overflowX: 'auto' }}>
                            <Menu
                                theme="dark"
                                mode="horizontal"
                                selectedKeys={activeSectionKey ? [activeSectionKey] : []}
                                items={visibleItems.map(({ key, label }) => ({ key, label }))}
                                onClick={({ key }) => setSelectedSection({ key, pathname })}
                                style={{ justifyContent: 'start' }}
                            />
                        </div>
                        <Space>
                            <Link to="/dashboard/profile">
                                <Button style={{ background: 'transparent', color: 'white' }} icon={<UserOutlined />}></Button>
                            </Link>
                            <Link to="/dashboard/messages">
                                <Badge count={unreadData?.unreadCount} size="small" offset={[10, 0]}>
                                    <Button style={{ background: 'transparent', color: 'white' }} icon={<MessageOutlined />}></Button>
                                </Badge>
                            </Link>
                            <Button style={{ background: 'transparent' }} icon={<LogoutOutlined />} onClick={handleLogout} danger></Button>
                        </Space>
                    </div>
                    {activeSection && (
                        <div style={{ padding: '0', background: 'white', display: 'flex', gap: '16px',  overflowX: 'auto' }}>
                            <Menu
                                theme="light"
                                mode="horizontal"
                                selectedKeys={activeChildKey ? [activeChildKey] : []}
                                items={activeSection.children.map((item) => ({
                                    key: item.key,
                                    label: item.path ? <Link to={item.path}>{item.label}</Link> : item.label,
                                    disabled: item.disabled,
                                    style: { padding: '18px 16px 8px' }
                                }))}
                                style={{ padding: '0 8px', flexGrow: 1, justifyContent: 'end', background: 'transparent' }}
                            />
                        </div>
                    )}
                </Header>
                <Content style={{ margin: 24 }}>
                    <Outlet />
                </Content>
            </Layout>
        </ConfigProvider>
    );
}
