class Solution {
public:
    vector<int> rotateRight(vector<int> nums, int k) {
        k %= static_cast<int>(nums.size());
        rotate(nums.begin(), nums.begin() + k, nums.end());
        return nums;
    }
};
