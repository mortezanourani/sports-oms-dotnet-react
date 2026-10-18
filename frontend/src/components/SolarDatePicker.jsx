import DatePickerModule from 'react-multi-date-picker';
import persian from 'react-date-object/calendars/persian.js';
import persianFa from 'react-date-object/locales/persian_fa.js';
import { Input } from 'antd';

const DatePicker = DatePickerModule.default || DatePickerModule;

export default function SolarDatePicker({ placeholder, ...props }) {
    return (
        <DatePicker
            calendar={persian}
            locale={persianFa}
            format="YYYY/MM/DD"
            calendarPosition="bottom-right"
            containerClassName="solar-date-picker"
            containerStyle={{ width: '100%' }}
            render={(value, openCalendar, handleValueChange) => (
                <Input
                    value={value}
                    placeholder={placeholder}
                    onFocus={openCalendar}
                    onChange={handleValueChange}
                />
            )}
            {...props}
        />
    );
}
