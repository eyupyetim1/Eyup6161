#include "bluecore/calibration.hpp"

namespace bluecore {

CalibrationResponse refuseUnverified(SensorId) {
    CalibrationResponse response{};
    response.accepted = 0;
    response.state = CalibrationState::NotAvailable;
    response.message = "Donanım doğrulanmadan kalibrasyon başlamaz.";
    return response;
}

CalibrationResponse CalibrationService::start(const CalibrationRequest& request) const {
    return refuseUnverified(request.id);
}

CalibrationState CalibrationService::state(SensorId) const { return CalibrationState::NotAvailable; }

bool CalibrationService::hasCoefficients(SensorId) const { return false; }

bool CalibrationService::apply(SensorId, float, float&) const { return false; }

bool CalibrationService::factoryReset() { return true; }

}  // namespace bluecore
