import axios from 'axios';

const otpService = {
  generateOtp: async (userName) => {
    const response = await axios.post('https://localhost:7214/v1/Otp/request-otp', { userName });
    const token = response.data;
    return { token };
  },
  verifyOtp: async (otp, bearerToken) => {
    const response = await axios.post(
      'https://localhost:7214/v1/Otp/validate-otp',
      { otp },
      {
        headers: {
          Authorization: `Bearer ${bearerToken}`,
        },
      }
    );
    return response.data;
  },
};

export default otpService;
