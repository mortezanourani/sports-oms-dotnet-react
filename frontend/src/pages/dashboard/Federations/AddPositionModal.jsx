import { Modal, Form, Input, Select, message } from 'antd';
import { useCreateFederationRep } from '../../../hooks/useFederations';
import { FEDERATION_POSITION_LABELS } from '../../../constants/labels';
import SolarDatePicker from '../../../components/SolarDatePicker';
import { toUtcDateTime } from '../../../utils/dates';

export default function AddPositionModal({ federation, open, onClose }) {
    const [form] = Form.useForm();
    const createFederationRepMutation = useCreateFederationRep();
    const handleFinish = async (values) => {
        try {
            await createFederationRepMutation.mutateAsync({
                federationId: federation.id,
                payload: {
                    username: values.username,
                    password: values.password || null,
                    firstName: values.firstName,
                    lastName: values.lastName,
                    position: values.position,
                    startDate: toUtcDateTime(values.startDate),
                    endDate: toUtcDateTime(values.endDate)
                }
            });

            message.success('سمت با موفقیت ایجاد و اختصاص داده شد');
            form.resetFields();
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
                    <Input placeholder="نام کاربری" />
                </Form.Item>

                <Form.Item
                    name="password"
                    label="کلمه عبور (فقط برای حساب جدید)"
                    extra="برای حساب جدید الزامی است؛ برای حساب موجود خالی بگذارید."
                >
                    <Input.Password placeholder="کلمه عبور" />
                </Form.Item>

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
                    <SolarDatePicker
                        placeholder="تاریخ شروع" 
                    />
                </Form.Item>

                <Form.Item 
                    name="endDate" 
                    label="تاریخ پایان (اختیاری)" 
                >
                    <SolarDatePicker
                        placeholder="تاریخ پایان (اختیاری)" 
                    />
                </Form.Item>

                <div style={{ textAlign: 'center', marginTop: 16 }}>
                    <Form.Item shouldUpdate={true}>
                        {() => (
                            <button
                                type="button"
                                onClick={() => form.submit()}
                                disabled={createFederationRepMutation.isPending}
                                style={{
                                    backgroundColor: '#00498d',
                                    color: '#fff',
                                    border: 'none',
                                    padding: '8px 24px',
                                    borderRadius: '4px',
                                    cursor: 'pointer'
                                }}
                            >
                                {createFederationRepMutation.isPending
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
