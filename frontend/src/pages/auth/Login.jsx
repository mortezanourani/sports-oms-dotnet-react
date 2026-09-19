import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Form, Input, Button, Card, Typography, Alert } from 'antd';
import { UserOutlined, LockOutlined } from '@ant-design/icons';
import { login } from '../../api/auth';

const { Title } = Typography;

export default function Login() {
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const handleSubmit = async (values) => {
        setError('');
        setLoading(true);
        try {
            await login(values.username, values.password);
            navigate('/dashboard/facilities');
        } catch (err) {
            setError(err.response?.data?.detail || 'نام کاربری یا رمز عبور اشتباه است');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ display: 'flex', justifyContent: 'center', marginTop: 80 }}>
            <Card style={{ width: 360 }}>
                <Title level={4} style={{ marginBottom: 24 }}>ورود به پنل</Title>
                {error && <Alert type="error" message={error} style={{ marginBottom: 16 }} showIcon />}
                <Form layout="vertical" onFinish={handleSubmit}>
                    <Form.Item
                        name="username"
                        label="نام کاربری"
                        rules={[{ required: true, message: 'وارد کردن نام کاربری الزامی است' }]}
                    >
                        <Input prefix={<UserOutlined />} />
                    </Form.Item>
                    <Form.Item
                        name="password"
                        label="رمز عبور"
                        rules={[{ required: true, message: 'وارد کردن رمز عبور الزامی است' }]}
                    >
                        <Input.Password prefix={<LockOutlined />} />
                    </Form.Item>
                    <Form.Item>
                        <Button type="primary" htmlType="submit" block loading={loading}>ورود</Button>
                    </Form.Item>
                </Form>
            </Card>
        </div>
    );
}
