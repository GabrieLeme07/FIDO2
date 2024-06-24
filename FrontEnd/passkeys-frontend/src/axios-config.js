import axios from 'axios';

const instance = axios.create({
  baseURL: 'http://localhost:7214',
  timeout: 5000, // tempo limite de 5 segundos
  headers: {
    'Content-Type': 'application/json',
    // outras configurações de cabeçalho, se necessário
  },
});

export default instance;
