#include "bluecore/power.hpp"

namespace bluecore {

PowerReport UnverifiedPowerMonitor::read() const {
    PowerReport report{};
    report.status = PowerStatus::Unknown;
    report.origin = DataOrigin::None;
    report.percentKnown = 0;
    report.voltageKnown = 0;
    return report;
}

}  // namespace bluecore
