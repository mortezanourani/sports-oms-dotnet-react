import { useState, useEffect } from 'react';
import { Modal, Form, Input, Select, message } from 'antd';
import { useCreateFederation, useFederations } from '../../../hooks/useFederations';
import { useProvinces, useCities } from '../../../hooks/useLocations';
import { useAuth } from '../../../hooks/useAuth';
import { FEDERATION_CATEGORY_LABELS } from '../../../constants/labels';

// parentFederation: pass this when the parent is already known (nested
// Mastery-tab usage). Omit it entirely for flat-table usage — the modal
// then renders its own parent-selection dropdown instead.
// targetProvinceId / targetCityId: the already-selected province/city
// from the flat table's own selector, used directly instead of asking
// the user to pick it again inside this modal.
export default function FederationFormModal({ level, parentFederation, targetProvinceId, targetCityId, open, onClose }) {
    const [form] = Form.useForm();
    const auth = useAuth();
    const [selectedProvinceId, setSelectedProvinceId] = useState(null);
    const createMutation = useCreateFederation();
    const { data: provinces } = useProvinces();
    const { data: cities } = useCities(selectedProvinceId);

    const needsParentPicker = !parentFederation && level !== 'National';

    // Only fetched when actually needed — a Provincial-level modal needs
    // Mastery federations to choose from; a City-level modal needs
    // Provincial federations scoped to the already-known province.
    const { data: nationalOptions } = useFederations(
        needsParentPicker && level === 'Provincial' ? { level: 'National' } : undefined
    );
    const { data: provincialOptions } = useFederations(
        needsParentPicker && level === 'City' ? { level: 'Provincial', provinceId: targetProvinceId } : undefined
    );

    const provinceOptions =
        auth.scopeLevel === 'Province' ? provinces?.filter((p) => p.id === auth.scopeProvinceId) : provinces;
    const cityOptions =
        auth.scopeLevel === 'City' ? cities?.filter((c) => c.id === auth.scopeCityId) : cities;

    useEffect(() => {
        if (open && level === 'Provincial' && !parentFederation && auth.scopeLevel === 'Province') {
            form.setFieldValue('provinceId', auth.scopeProvinceId);
            setSelectedProvinceId(auth.scopeProvinceId);
        }
        if (open && level === 'Provincial' && parentFederation && auth.scopeLevel === 'Province') {
            form.setFieldValue('provinceId', auth.scopeProvinceId);
            setSelectedProvinceId(auth.scopeProvinceId);
        }
        if (open && level === 'City') {
            const province = parentFederation?.provinceId ?? targetProvinceId;
            setSelectedProvinceId(province);
            if (auth.scopeLevel === 'City') form.setFieldValue('cityId', auth.scopeCityId);
            else if (targetCityId) form.setFieldValue('cityId', targetCityId);
        }
    }, [open, level, parentFederation, targetProvinceId, targetCityId, auth, form]);

    const handleFinish = async (values) => {
        let payload;
        if (level === 'National') {
            payload = { level: 'National', name: values.name, category: values.category };
        } else if (level === 'Provincial') {
            const parentId = parentFederation ? parentFederation.id : values.parentFederationId;
            payload = { level: 'Provincial', parentFederationId: parentId, provinceId: values.provinceId };
        } else {
            const parentId = parentFederation ? parentFederation.id : values.parentFederationId;
            payload = { level: 'City', parentFederationId: parentId, cityId: values.cityId };
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
        Mastery: 'افزودن فدراسیون کشوری',
        Provincial: parentFederation
            ? `افزودن فدراسیون استانی زیرمجموعه «${parentFederation.displayName}»`
            : 'افزودن فدراسیون استانی',
        City: parentFederation
            ? `افزودن فدراسیون شهری زیرمجموعه «${parentFederation.displayName}»`
            : 'افزودن فدراسیون شهری',
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

                {level === 'Provincial' && needsParentPicker && (
                    <Form.Item name="parentFederationId" label="فدراسیون کشوری" rules={[{ required: true }]}>
                        <Select
                            placeholder="انتخاب رشته"
                            options={nationalOptions?.map((f) => ({ value: f.id, label: f.displayName }))}
                        />
                    </Form.Item>
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

                {level === 'City' && needsParentPicker && (
                    <Form.Item name="parentFederationId" label="فدراسیون استانی" rules={[{ required: true }]}>
                        <Select
                            placeholder={!selectedProvinceId ? 'ابتدا استان را انتخاب کنید' : 'انتخاب رشته'}
                            disabled={!selectedProvinceId}
                            options={provincialOptions?.map((f) => ({ value: f.id, label: f.displayName }))}
                        />
                    </Form.Item>
                )}
                {level === 'City' && (
                    <Form.Item name="cityId" label="شهر" rules={[{ required: true }]}>
                        <Select
                            disabled={auth.scopeLevel === 'City' || !!targetCityId}
                            options={cityOptions?.map((c) => ({ value: c.id, label: c.name }))}
                        />
                    </Form.Item>
                )}
            </Form>
        </Modal>
    );
}
