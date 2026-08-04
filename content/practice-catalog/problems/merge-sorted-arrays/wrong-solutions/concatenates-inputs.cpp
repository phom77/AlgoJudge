class Solution {
public:
    vector<int> mergeSorted(vector<int>& first, vector<int>& second) {
        vector<int> result = first;
        result.insert(result.end(), second.begin(), second.end());
        return result;
    }
};
