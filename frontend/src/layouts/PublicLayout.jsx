import { Outlet, Link } from 'react-router-dom';
import { Layout, Menu, ConfigProvider } from 'antd';

const { Header, Content, Footer } = Layout;

const items = [
    { key: '/', label: <Link style={{ fontWeight: 'bold' }} to="/">سامانه آمار ورزشی</Link> },
    { key: '/athletes', label: <Link to="/">ورزشکاران</Link> },
    { key: '/champions', label: <Link to="/champions">قهرمانان</Link> },
    { key: '/facilities', label: <Link to="/facilities">اماکن ورزشی</Link> },
];

const loginItems = [

];

export default function PublicLayout() {
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
                        darkItemColor: '#deebf8',
                        darkItemSelectedBg: '#2069ad',
                        darkItemSelectedColor: '#fff',
                    },
                },
            }}
        >
            <Layout style={{ minHeight: '100vh' }}>
                <Header style={{ display: 'flex', alignItems: 'center' }}>
                    <Menu theme="dark" mode="horizontal" items={items} style={{ flex: 1, justifyContent: 'start' }} />
                    <Link to="/login" style={{ color: 'white' }}>پنل کاربری</Link>
                </Header>
                <Content style={{ padding: '24px 48px' }}>
                    <Outlet />
                </Content>
                <Footer style={{ textAlign: 'center' }}>سامانه مدیریت ورزش</Footer>
            </Layout>
        </ConfigProvider>
    );
}
