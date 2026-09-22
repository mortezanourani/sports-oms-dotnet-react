import { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, message } from 'antd';
import { useCreateFederation } from '../../../hooks/useFederations';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { useAuth } from '../../../hooks/useAuth';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';

// level: 'National' | 'Provincial' | 'City'
// parentFederation: the National federation (for Provincial) or the
// Provincial federation (for City) this new one will be created under —
// undefined when level === 'National'.
export default function FederationFormModal({ level, parentFederation, open, onClose }) {
    const [form] = Form.useForm();
    const auth = useAuth();
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const createMutation = useCreateFederation();
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    // Restricts the province dropdown to what THIS user is actually allowed
    // to create in — a Province-scoped user only ever sees their own
    // province as an option, rather than a full list they'd only get a 403
    // from the backend for picking anything else in.
    const provinceOptions =
        auth.scopeLevel === 'Province'
            ? provinces?.filter((p) => p.id === auth.scopeProvinceId)
            : provinces;

    useEffect(() => {
        if (open && level === 'Provincial' && auth.scopeLevel === 'Province') {
            form.setFieldValue('provinceId', auth.scopeProvinceId);
            setSelectedProvinceId(auth.scopeProvinceId);
        }
        if (open && level === 'City') {
            // For City creation, the province is always implied by the parent
            // Provincial federation — never a free choice, so the city dropdown
            // is scoped immediately without waiting for a province selection.
            setSelectedProvinceId(parentFederation?.provinceId);
            if (auth.scopeLevel === 'City') {
                form.setFieldValue('cityId', auth.scopeCityId);
            }
        }
    }, [open, level, auth, parentFederation, form]);

    const cityOptions =
        auth.scopeLevel === 'City'
            ? cities?.filter((c) => c.id === auth.scopeCityId)
            : cities;

    const handleFinish = async (values) => {
        let payload;
        if (level === 'National') {
            payload = { level: 'National', name: values.name, category: values.category };
        } else if (level === 'Provincial') {
            payload = { level: 'Provincial', parentFederationId: parentFederation.id, provinceId: values.provinceId };
        } else {
            payload = { level: 'City', parentFederationId: parentFederation.id, cityId: values.cityId };
        }

        try {
            await createMutation.mutateAsync(payload);
            message.success('با موفقیت ایجاد شد');
            form.resetFields();
            onClose();
        } catch (err) {
            message.error(err.response?.data?.detail || 'خطایی رخ داد');
        }
    };

    const titles = {
        National: 'افزودن فدراسیون کشوری',
        Provincial: `افزودن فدراسیون استانی زیرمجموعه «${parentFederation?.displayName}»`,
        City: `افزودن فدراسیون شهری زیرمجموعه «${parentFederation?.displayName}»`,
    };

    return (
        <Modal
            title={titles[level]}
            open={open}
            onCancel={onClose}
            onOk={() => form.submit()}
            confirmLoading={createMutation.isPending}
            okText="ایجاد"
            cancelText="انصراف"
            destroyOnClose
        >
            <Form form={form} layout="vertical" onFinish={handleFinish}>
                {level === 'National' && (
                    <>
                        <Form.Item name="name" label="نام فدراسیون" rules={[{ required: true }]}>
                            <Input />
                        </Form.Item>
                        <Form.Item name="category" label="دسته‌بندی" rules={[{ required: true }]}>
                            <Select options={Object.entries(FEDERATION_CATEGORY_LABELS).map(([value, label]) => ({ value, label }))} />
                        </Form.Item>
                    </>
                )}

                {level === 'Provincial' && (
                    <Form.Item name="provinceId" label="استان" rules={[{ required: true }]}>
                        <Select
                            disabled={auth.scopeLevel === 'Province'}
                            options={provinceOptions?.map((p) => ({ value: p.id, label: p.name }))}
                            onChange={setSelectedProvinceId}
                        />
                    </Form.Item>
                )}

                {level === 'City' && (
                    <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                        <Select
                            disabled={auth.scopeLevel === 'City'}
                            options={cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                        />
                    </Form.Item>
                )}
            </Form>
        </Modal>
    );
}
