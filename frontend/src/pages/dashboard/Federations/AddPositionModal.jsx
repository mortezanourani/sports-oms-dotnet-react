import { useEffect, useState } from 'react';
import { Modal, Form, Input, Select, DatePicker, message } from 'antd';
import { useCreateUser, useAssignFederationPosition, useUserByUsername } from '../../../hooks/useUsers';
import { FEDERATION_POSITION_LABELS } from '../../../constants/labels';
import { useAuth } from '../../../hooks/useAuth';

export default function AddPositionModal({ federation, open, onClose }) {
    const [form] = Form.useForm();
    const auth = useAuth();
    
    const [selectedUsername, setSelectedUsername] = useState(null);
    const { data: existingUser, isLoading: isUserLoading } = useUserByUsername(selectedUsername);
    
    const createUserMutation = useCreateUser();
    const assignPositionMutation = useAssignFederationPosition();

    // Determine province and city based on user scope and federation context
    const getProvinceId = () => {
        if (auth.scopeLevel === 'Province') {
            return auth.scopeProvinceId;
        }
        if (auth.scopeLevel === 'City') {
            // For city-scoped users, we need to get the province from their city
            return null; // This should be populated from backend/city data
        }
        // For Mastery users, use the federation's province
        return federation?.provinceId || null;
    };

    const getCityId = () => {
        if (auth.scopeLevel === 'City') {
            return auth.scopeCityId;
        }
        // For other scopes, use the federation's city if available
        return federation?.cityId || null;
    };

    useEffect(() => {
        if (open && federation) {
            // Set province based on user scope and federation context
            if (auth.scopeLevel === 'Province') {
                // setSelectedProvinceId(auth.scopeProvinceId); // Not needed anymore since province is auto-determined
            } else if (federation.provinceId) {
                // setSelectedProvinceId(federation.provinceId); // Not needed anymore since province is auto-determined
            }
        }
    }, [open, federation, auth]);

    const handleUsernameChange = (e) => {
        const username = e.target.value;
        setSelectedUsername(username);
    };

    const isExistingUser = existingUser && !isUserLoading;

    const handleFinish = async (values) => {
        try {
            // Determine scope based on user's authorization level
            const userScopeLevel = auth.scopeLevel;
            const userProvinceId = getProvinceId();
            const userCityId = getCityId();

            let targetUserId;

            // Check if user already exists
            if (isExistingUser) {
                // User exists, use their ID
                targetUserId = existingUser.id;
                message.info('کاربر existing است - فقط سمت اختصاص داده می‌شود');
            } else {
                // User doesn't exist, create them first
                const createUserPayload = {
                    username: values.username,
                    password: values.password,
                    firstName: values.firstName,
                    lastName: values.lastName,
                    role: 'FederationRep',
                    scopeLevel: userScopeLevel,
                    scopeProvinceId: userProvinceId,
                    scopeCityId: userCityId
                };

                const newUser = await createUserMutation.mutateAsync(createUserPayload);
                targetUserId = newUser.id;
            }

            // Step 2: Assign the user to the federation position
            const assignPayload = {
                position: values.position,
                startDate: values.startDate,
                endDate: values.endDate || null
            };

            await assignPositionMutation.mutateAsync({
                userId: targetUserId,
                payload: assignPayload
            });

            message.success('سمت با موفقیت ایجاد و اختصاص داده شد');
            form.resetFields();
            setSelectedUsername(null);
            onClose();
        } catch (err) {
            console.error('Error adding position:', err);
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const handleCancel = () => {
        form.resetFields();
        onClose();
    };

    return (
        <Modal
            title={`افزودن سمت به فدراسیون ${federation?.displayName}`}
            open={open}
            onCancel={handleCancel}
            footer={null}
            width={500}
        >
            <Form 
                form={form} 
                layout="vertical" 
                onFinish={handleFinish}
                preserve={false}
            >
                <Form.Item 
                    name="username" 
                    label="نام کاربری" 
                    rules={[{ required: true, message: 'لطفاً نام کاربری را وارد کنید' }]}
                >
                    <Input 
                        placeholder="نام کاربری" 
                        onChange={handleUsernameChange}
                    />
                </Form.Item>

                {/* Show password field only when user doesn't exist (not loading) */}
                {!isExistingUser && (
                    <Form.Item 
                        name="password" 
                        label="کلمه عبور" 
                        rules={[{ required: !isExistingUser, message: 'لطفاً کلمه عبور را وارد کنید' }]}
                    >
                        <Input.Password placeholder="کلمه عبور" />
                    </Form.Item>
                )}

                <Form.Item 
                    name="firstName" 
                    label="نام" 
                    rules={[{ required: true, message: 'لطفاً نام را وارد کنید' }]}
                >
                    <Input placeholder="نام" />
                </Form.Item>

                <Form.Item 
                    name="lastName" 
                    label="نام خانوادگی" 
                    rules={[{ required: true, message: 'لطفاً نام خانوادگی را وارد کنید' }]}
                >
                    <Input placeholder="نام خانوادگی" />
                </Form.Item>

                <Form.Item 
                    name="position" 
                    label="سمت" 
                    rules={[{ required: true, message: 'لطفاً سمت را انتخاب کنید' }]}
                >
                    <Select 
                        placeholder="انتخاب سمت"
                        options={Object.entries(FEDERATION_POSITION_LABELS).map(([value, label]) => ({
                            value,
                            label
                        }))}
                    />
                </Form.Item>

                <Form.Item 
                    name="startDate" 
                    label="تاریخ شروع" 
                    rules={[{ required: true, message: 'لطفاً تاریخ شروع را انتخاب کنید' }]}
                >
                    <DatePicker 
                        format="YYYY-MM-DD" 
                        placeholder="تاریخ شروع" 
                        style={{ width: '100%' }} 
                    />
                </Form.Item>

                <Form.Item 
                    name="endDate" 
                    label="تاریخ پایان (اختیاری)" 
                >
                    <DatePicker 
                        format="YYYY-MM-DD" 
                        placeholder="تاریخ پایان (اختیاری)" 
                        style={{ width: '100%' }} 
                    />
                </Form.Item>

                <div style={{ textAlign: 'center', marginTop: 16 }}>
                    <Form.Item shouldUpdate={true}>
                        {() => (
                            <button
                                type="button"
                                onClick={() => form.submit()}
                                disabled={createUserMutation.isPending || assignPositionMutation.isPending}
                                style={{
                                    backgroundColor: '#00498d',
                                    color: '#fff',
                                    border: 'none',
                                    padding: '8px 24px',
                                    borderRadius: '4px',
                                    cursor: 'pointer'
                                }}
                            >
                                {createUserMutation.isPending || assignPositionMutation.isPending 
                                    ? 'در حال انجام...' 
                                    : 'ثبت'}
                            </button>
                        )}
                    </Form.Item>
                </div>
            </Form>
        </Modal>
    );
}
