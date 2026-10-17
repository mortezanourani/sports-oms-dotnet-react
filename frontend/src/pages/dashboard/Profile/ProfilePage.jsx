import { useEffect } from 'react';
import { Alert, Button, Card, Col, Form, Input, Row, Typography, message } from 'antd';
import { useChangePassword, useProfile, useUpdateProfile } from '../../../hooks/useProfile';

const { Title } = Typography;

function errorText(error, fallback) {
    return error.response?.data?.detail || fallback;
}

export default function ProfilePage() {
    const [profileForm] = Form.useForm();
    const [passwordForm] = Form.useForm();
    const profile = useProfile();
    const updateProfile = useUpdateProfile();
    const changePassword = useChangePassword();

    useEffect(() => {
        if (profile.data) {
            profileForm.setFieldsValue({
                firstName: profile.data.firstName,
                lastName: profile.data.lastName,
            });
        }
    }, [profile.data, profileForm]);

    const handleProfileSubmit = async (values) => {
        try {
            await updateProfile.mutateAsync(values);
            message.success('اطلاعات پروفایل با موفقیت به‌روزرسانی شد');
        } catch (error) {
            message.error(errorText(error, 'به‌روزرسانی پروفایل با خطا مواجه شد'));
        }
    };

    const handlePasswordSubmit = async (values) => {
        try {
            await changePassword.mutateAsync({
                currentPassword: values.currentPassword,
                newPassword: values.newPassword,
            });
            passwordForm.resetFields();
            message.success('رمز عبور با موفقیت تغییر کرد');
        } catch (error) {
            message.error(errorText(error, 'تغییر رمز عبور با خطا مواجه شد'));
        }
    };

    return (
        <div>
            <Title level={4} style={{ marginTop: 0, color: '#00498d' }}>پروفایل کاربری</Title>

            {profile.isError && (
                <Alert
                    type="error"
                    showIcon
                    message={errorText(profile.error, 'دریافت اطلاعات پروفایل با خطا مواجه شد')}
                    style={{ marginBottom: 16 }}
                />
            )}

            <Row gutter={[16, 16]}>
                <Col xs={24} md={24} lg={12}>
                    <Card title="اطلاعات کاربری" style={{ height: '100%' }}>
                        <Form
                            form={profileForm}
                            layout="vertical"
                            onFinish={handleProfileSubmit}
                            disabled={profile.isLoading || profile.isError}
                        >
                            <Form.Item label="نام کاربری">
                                <Input value={profile.data?.username} disabled />
                            </Form.Item>
                            <Form.Item
                                name="firstName"
                                label="نام"
                                rules={[{ required: true, whitespace: true, message: 'نام الزامی است' }]}
                            >
                                <Input autoComplete="given-name" />
                            </Form.Item>
                            <Form.Item
                                name="lastName"
                                label="نام خانوادگی"
                                rules={[{ required: true, whitespace: true, message: 'نام خانوادگی الزامی است' }]}
                            >
                                <Input autoComplete="family-name" />
                            </Form.Item>
                            <Button type="primary" htmlType="submit" loading={updateProfile.isPending}>
                                ذخیره تغییرات
                            </Button>
                        </Form>
                    </Card>
                </Col>
                <Col xs={24} md={24} lg={12}>
                    <Card title="تغییر رمز عبور" style={{ height: '100%' }}>
                        <Form
                            form={passwordForm}
                            layout="vertical"
                            onFinish={handlePasswordSubmit}
                        >
                            <Form.Item
                                name="currentPassword"
                                label="رمز عبور فعلی"
                                rules={[{ required: true, message: 'رمز عبور فعلی الزامی است' }]}
                            >
                                <Input.Password autoComplete="current-password" />
                            </Form.Item>
                            <Form.Item
                                name="newPassword"
                                label="رمز عبور جدید"
                                rules={[{ required: true, message: 'رمز عبور جدید الزامی است' }]}
                            >
                                <Input.Password autoComplete="new-password" />
                            </Form.Item>
                            <Form.Item
                                name="confirmPassword"
                                label="تکرار رمز عبور جدید"
                                dependencies={['newPassword']}
                                rules={[
                                    { required: true, message: 'تکرار رمز عبور جدید الزامی است' },
                                    ({ getFieldValue }) => ({
                                        validator(_, value) {
                                            if (!value || getFieldValue('newPassword') === value) {
                                                return Promise.resolve();
                                            }
                                            return Promise.reject(new Error('رمزهای عبور جدید یکسان نیستند'));
                                        },
                                    }),
                                ]}
                            >
                                <Input.Password autoComplete="new-password" />
                            </Form.Item>
                            <Button type="primary" htmlType="submit" loading={changePassword.isPending}>
                                تغییر رمز عبور
                            </Button>
                        </Form>
                    </Card>
                </Col>
            </Row>
        </div>
    );
}
