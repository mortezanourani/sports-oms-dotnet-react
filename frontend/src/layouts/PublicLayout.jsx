import { Outlet, Link } from 'react-router-dom';
import { Layout, Menu } from 'antd';

const { Header, Content, Footer } = Layout;

const items = [
    { key: '/', label: <Link to="/">ورزشکاران</Link> },
    { key: '/champions', label: <Link to="/champions">قهرمانان</Link> },
    { key: '/facilities', label: <Link to="/facilities">اماکن ورزشی</Link> },
    { key: '/login', label: <Link to="/login">ورود</Link> },
];

export default function PublicLayout() {
    return (
        <Layout style={{ minHeight: '100vh' }}>
            <Header style={{ display: 'flex', alignItems: 'center' }}>
                <div style={{ color: 'white', fontWeight: 'bold', marginInlineEnd: 32 }}>
                    سامانه آمار ورزشی
                </div>
                <Menu theme="dark" mode="horizontal" items={items} style={{ flex: 1, justifyContent: 'end' }} />
            </Header>
            <Content style={{ padding: '24px 48px' }}>
                <Outlet />
            </Content>
            <Footer style={{ textAlign: 'center' }}>سامانه مدیریت ورزش</Footer>
        </Layout>
    );
}
