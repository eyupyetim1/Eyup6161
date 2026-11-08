#include "bluecore/radio.hpp"

namespace bluecore {

WifiReport DormantRadio::read() const {
    WifiReport report{};
    report.status = WifiStatus::Disabled;
    report.origin = DataOrigin::None;
    report.ssid[0] = '\0';
    return report;
}

}  // namespace bluecore
