class Solution {
public:
    vector<int> mergeSorted(vector<int>& first, vector<int>& second) {
        vector<int> result;
        result.reserve(first.size() + second.size());
        size_t left = 0;
        size_t right = 0;
        while (left < first.size() && right < second.size()) {
            if (first[left] <= second[right]) result.push_back(first[left++]);
            else result.push_back(second[right++]);
        }
        result.insert(result.end(), first.begin() + left, first.end());
        result.insert(result.end(), second.begin() + right, second.end());
        return result;
    }
};
